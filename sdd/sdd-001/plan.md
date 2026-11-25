# Plan: caché de consultas con FusionCache + Redis

- **Estado:** implementado y validado (2026-11-24)
- **Fecha:** 2026-11-23
- **Servicio:** `Bitai.LDAPGateway`
- **Skill de referencia:** `dotnet-redis-fusioncache-wolv`

## 1. Objetivo

Cachear las consultas al directorio (las operaciones LDAP caras) usando FusionCache con Redis como L2 y un backplane para coordinar la L1 de las distintas réplicas. Las mutaciones deben invalidar el caché del perfil afectado inmediatamente después de completarse la escritura en LDAP.

## 2. Preflight del repositorio

| Punto | Hallazgo |
|---|---|
| Framework | .NET 10 (`net10.0`), nullable e implicit usings activos. |
| Paquetes | Wolverine `6.44.0`, FluentValidation `12.1.0`, NSwag `14.7.1`. No hay FusionCache, Redis ni `HybridCache`. |
| Arquitectura | Clean Architecture: `Domain`, `Application`, `Infrastructure`, `Api`. |
| Handlers | Wolverine con código generado **commiteado** en `src/Api/Internal/Generated/WolverineHandlers` y `TypeLoadMode.Static` en Production. Cualquier cambio de constructor exige regenerar el código. |
| Persistencia | No hay EF Core ni base de datos. LDAP es la fuente de verdad, externa y mutable por otros actores. |
| Outbox | No aplica el patrón post-commit con outbox durable del skill: no hay transacción local ni message store. |
| Consultas | 8 consultas LDAP, 3 consultas de configuración servidas desde `IOptions` y 2 comandos de autenticación. |
| Mutaciones | 4 comandos: crear, deshabilitar, eliminar usuario y establecer contraseña. |
| Tests | xUnit + Moq; funcionales con `WebApplicationFactory`; entornos `Testing` y `Development`. |
| Despliegue | No hay manifiestos ni endpoints de health todavía; el servicio está descrito como stateless y se asume multi-instancia. |

## 3. Desviación respecto del skill (explícita)

El skill exige invalidar después del commit con un outbox durable. Aquí no existe commit ni base de datos, por lo que se aplica esta adaptación:

1. La invalidación se ejecuta **después** de que la operación LDAP devuelve éxito y **antes** de responder al cliente.
2. No hay reintento durable: si el proceso muere entre la escritura LDAP y la invalidación, o Redis está caído, la ventana la cubre el TTL.
3. Los cambios realizados en Active Directory **por fuera** de este gateway no generan invalidación; solo los acota el TTL de L2.

Estas ventanas quedan documentadas en el README y no se presentan como consistencia fuerte.

## 4. Decisiones

| Tema | Decisión |
|---|---|
| Topología | Multi-instancia, opción A del skill: backplane Pub/Sub + contador de generación + reconciliador + readiness. |
| Frescura | L1 = 30 s, L2 = 2 min para consultas de directorio; valores específicos para `Get*Parents` y resultados no encontrados. |
| Alcance | Solo las 8 consultas LDAP. Autenticación, perfiles y catálogos quedan fuera. |
| Redis ausente | `Caching:RedisConnectionString` obligatoria si `Caching:Enabled` es `true`. Si Redis está caído en runtime, el servicio arranca y sirve directo desde LDAP (`AbortOnConnectFail=false`, timeouts acotados). |
| Pruebas | Unitarias + funcionales con caché deshabilitada. Sin Testcontainers. |
| Configuración | `AddCaching` se invoca desde `AddInfrastructure`; `Program.cs` solo añade los endpoints de health. |

## 5. Garantías y ventanas de staleness

| Escenario | Qué protege | Ventana residual |
|---|---|---|
| Lectura normal | L1 → L2 → LDAP | — |
| Mutación a través del gateway | Invalidación del tag del perfil antes de responder | 0 para read-your-writes en réplicas conectadas |
| Notificación del backplane perdida | Reconciliador limpia la L1 local al detectar hueco | ≤ 2 × `ReconcileInterval` (60 s) |
| Réplica desconectada y reconectada | `ConnectionRestored` → flush local | ≈ 1 s tras la reconexión |
| Crash entre escritura LDAP e invalidación, o Redis caído | Ninguno (no hay outbox) | Hasta el TTL de L2 (2 min) |
| Cambio externo en AD | Ninguno | Hasta el TTL de L2 (2 min) |
| Fallo 5xx/502 de LDAP | No se cachea | — |
| No encontrado (404 / `null`) | Caché negativa corta | L1 10 s / L2 30 s |

## 6. Diseño

### 6.1 Operaciones cacheadas

| Operación | Handler | Clase de TTL |
|---|---|---|
| `SearchUsers` | `SearchUsersQueryHandler` | Búsqueda |
| `SearchGroups` | `SearchGroupsQueryHandler` | Búsqueda |
| `SearchDirectory` | `SearchDirectoryQueryHandler` | Búsqueda |
| `GetUserByIdentifier` | `GetUserByIdentifierQueryHandler` | Entrada |
| `GetGroupByIdentifier` | `GetGroupByIdentifierQueryHandler` | Entrada |
| `GetDirectoryEntryByIdentifier` | `GetDirectoryEntryByIdentifierQueryHandler` | Entrada |
| `GetUserParents` | `GetUserParentsQueryHandler` | Padres |
| `GetGroupParents` | `GetGroupParentsQueryHandler` | Padres |

**No se cachea:**

- `AuthenticateCommand` y `AuthenticateWithoutUserLookupCommand`: credenciales, estado de cuenta y bloqueos.
- `GetProfiles`, `GetProfileIds`, `GetProfileById`: se sirven de `IOptionsMonitor`, sin I/O.
- `GetCatalogTypes`: lista constante en memoria.
- Consultas cuyo atributo de filtro sea `userPassword` o `unicodePwd`.

### 6.2 Políticas de expiración (`CachePolicies`)

| Clase | L1 (`Duration`) | L2 (`DistributedCacheDuration`) |
|---|---|---|
| Búsqueda | 30 s | 2 min |
| Entrada | 30 s | 2 min |
| Padres | 60 s | 5 min |
| No encontrado | 10 s | 30 s |

La caché negativa se usa cuando el conector devuelve `Error.StatusCode == 404` o, en consultas de detalle, cuando el resultado es exitoso con `Value == null`.

### 6.3 Claves y tags (`CacheKeys` / `CacheTags`)

- **Claves** deterministas, construidas con un helper que codifica cada componente con longitud (`$"{value.Length}:{value}"`), unido por `\u001F`, y hashea el conjunto con SHA-256:
  - `CacheKeys.SearchUsers(SearchUsersQuery)`
  - `CacheKeys.SearchGroups(SearchGroupsQuery)`
  - `CacheKeys.SearchDirectory(SearchDirectoryQuery)`
  - `CacheKeys.User(GetUserByIdentifierQuery)`
  - `CacheKeys.Group(GetGroupByIdentifierQuery)`
  - `CacheKeys.Entry(GetDirectoryEntryByIdentifierQuery)`
  - `CacheKeys.UserParents(GetUserParentsQuery)`
  - `CacheKeys.GroupParents(GetGroupParentsQuery)`

  Forma lógica: `{operacion}:{sha256hex}`. El `CacheKeyPrefix` de FusionCache aporta servicio, entorno y versión; no se repiten en la clave.

- **Componentes**: perfil normalizado a minúsculas, catálogo, operación, atributos, valores normalizados (`Trim()` + `ToLowerInvariant()` porque AD compara sin distinguir mayúsculas) y `RequiredAttributeSet` (cambia la forma del DTO), más `UserMustExists` / `GroupMustExist` / `UserMustExist` / `GroupMustExist` cuando aplica.
- **Sin PII en claves ni tags**: identificadores y valores de filtro solo existen dentro del hash. Los tags llevan únicamente el id de perfil.
- **Tag de invalidación**: `ldap:profile:{profileIdEnMinusculas}`. Un solo tag coarse por perfil: cualquier mutación invalida todas las lecturas de ese perfil (ambos catálogos), que es la estrategia correcta para listas de búsqueda.
- **Límite de payload**: si una búsqueda devuelve más de 200 entradas, el resultado no se escribe en Redis: se mantiene solo en la L1 del nodo (`SkipDistributedCacheWrite`), para acotar la memoria de L2. El límite es la constante `CachePolicies.MaxEntriesToCache`; cambiarlo requiere recompilar.

### 6.4 Modelo cacheado

`Result<T>` no es serializable por System.Text.Json (constructor privado). Se cachea un sobre propio:

```csharp
public sealed record CachedResult<T>(bool IsSuccess, T? Value, string? ErrorCode, string? ErrorMessage, int StatusCode)
{
    public static CachedResult<T> Success(T? value) => new(true, value, null, null, 200);
    public static CachedResult<T> NotFound(Error error) => new(false, default, error.Code, error.Message, error.StatusCode);
}
```

Reglas:

- Éxito → se cachea con el TTL de la clase.
- 404 o `null` en detalle → sobre de no encontrado con TTL corto.
- Cualquier otro fallo → la factory lanza `CacheBypassException` para que FusionCache **no** lo guarde. `DirectoryCache` la captura incluso cuando FusionCache la envuelve (recorre `InnerException`). Se configura `FusionCacheOptions.ReThrowOriginalExceptions = true` para conservar la excepción original.
- Si el resultado excede `CachePolicies.MaxEntriesToCache`, se marca `SkipDistributedCacheWrite` para no enviarlo a Redis.
- `LdapEntryDto` pasa a colecciones de solo lectura (`IReadOnlyList<string>?` para `member`, `memberOf`, `objectClass`; `IReadOnlyList<LdapEntryDto>?` para `memberOfEntries`) para poder compartir la instancia de L1 sin clones defensivos. El JSON de la API no cambia.

### 6.5 Lectura

Nueva base en Application. Los handlers no reciben `IFusionCache` directamente: reciben la fachada `DirectoryCache`, que centraliza claves, políticas, caché negativa e invalidación. Es una desviación deliberada del patrón del skill (que inyecta `IFusionCache` en cada handler) para no repetir la misma lógica en 12 constructores.

```csharp
public sealed class DirectoryCache
{
    public Task<CachedQueryResult<T>> GetOrSetAsync<T>(
        string cacheKey,
        string serverProfile,
        FusionCacheEntryOptions entryOptions,
        bool cacheNullAsNotFound,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken,
        Func<T, bool>? allowDistributedCache = null);

    public Task InvalidateProfileAsync(string operationName, string serverProfile);
}

public abstract class DirectoryLdapHandlerBase : LdapHandlerBase
{
    protected DirectoryLdapHandlerBase(IDomainEventPublisher domainEventPublisher, DirectoryCache cache);

    protected Task<Result<T>> ExecuteCachedQueryAsync<T>(...);
    protected Task<Result<T>> ExecuteMutationAsync<T>(...);
    protected Task<Result> ExecuteMutationAsync(...);
}
```

Comportamiento de `ExecuteCachedQueryAsync`:

1. `_cache.GetOrSetAsync<CachedResult<T>>(cacheKey, factory, entryOptions, tags: [CacheTags.Profile(context.ServerProfile)], token: ct)`.
2. La factory ejecuta la operación; aplica las reglas de la sección 6.4.
3. Devuelve `Result<T>` y publica `OperationCompletedDomainEvent` como hoy (también en cache hit, indicando `(cached)` en el mensaje).
4. `LdapHandlerBase` se mantiene intacta para autenticación, perfiles y catálogos.

### 6.6 Invalidación

`ExecuteMutationAsync`:

1. Ejecuta la operación LDAP.
2. Si `result.IsSuccess`, llama a `ICacheInvalidator.InvalidateAsync([], [CacheTags.Profile(context.ServerProfile)], CancellationToken.None)`.
   - `CancellationToken.None` a propósito: un cliente que aborta la petición no debe saltarse la invalidación.
3. Si la invalidación falla, se registra `LogWarning` con perfil y operación (sin datos sensibles) y **no** se altera el resultado del comando.
4. Publica el evento de operación completada.

Handlers que pasan a `ExecuteMutationAsync`: `CreateMsAdUserCommandHandler`, `DisableMsAdUserCommandHandler`, `DeleteMsAdUserCommandHandler`, `SetMsAdUserPasswordCommandHandler`.

### 6.7 Multi-instancia (opción A)

- Un único `IConnectionMultiplexer` compartido por L2, backplane y contador de generación; `AbortOnConnectFail=false`, `ConnectTimeout`/`SyncTimeout`/`AsyncTimeout` de 5 000 ms, `KeepAlive` 60 s y `ClientName` derivado de `KeyPrefix`.
- L2: `IDistributedCache` sobre ese multiplexer (`Microsoft.Extensions.Caching.StackExchangeRedis`).
- Backplane: `RedisBackplane` con factory que reutiliza el multiplexer.
- `FusionCacheInvalidator`:
  1. `INCR` de `GenerationKey`.
  2. `RemoveAsync` de claves y `RemoveByTagAsync` de tags.
  3. `PUBLISH` de la generación en `GenerationChannel`.
- `CacheReconciler` (singleton + hosted service):
  - Suscribe a `GenerationChannel` antes de leer el contador.
  - `ConnectionRestored`, evento malformado, hueco confirmado (dos comprobaciones consecutivas) o exceso de generaciones fuera de orden → `MarkDirty()`.
  - Flush **solo local** con `ClearAsync(allowFailSafe: false, LocalOnly, ct)` y opciones con `SkipBackplaneNotifications`, `SkipDistributedCacheRead`, `SkipDistributedCacheWrite`.
  - Loop con `PeriodicTimer(1 s)`, debounce mínimo de 1 s, `ReconcileInterval` para comprobar el contador.
  - Métricas: `cache_reconciler_ready` (gauge), `cache_reconcile_flushes` y `cache_generation_malformed` (contadores) en el meter `Bitai.LDAPGateway.Caching`.
- `CacheReconcilerHealthCheck` con tag `ready`: `Unhealthy` si nunca se suscribió, `Degraded` si Redis no está disponible o hay un flush pendiente, `Healthy` en el resto.
- `/health/live` no depende del reconciliador.

### 6.8 Resiliencia con Redis caído

- Lecturas: FusionCache cae a la factory (LDAP) cuando L2 o el backplane fallan.
- Inicialización: el host arranca aunque Redis no responda; el reconciliador reintenta la suscripción mientras la readiness reporta `Unhealthy`/`Degraded`.
- La connection string es obligatoria al arrancar si `Caching:Enabled` es `true` (validación `ValidateOnStart`).

## 7. Cambios por capa

### 7.1 Paquetes

| Proyecto | Paquete | Versión |
|---|---|---|
| Application | `ZiggyCreatures.FusionCache` | `2.10.0` |
| Application | `Microsoft.Extensions.Logging.Abstractions` | `10.0.15` (subida desde `10.0.0`: Wolverine 6.44 exige >= `10.0.14` y el restore trataba NU1605 como error) |
| Infrastructure | `ZiggyCreatures.FusionCache` | `2.10.0` |
| Infrastructure | `ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis` | `2.10.0` |
| Infrastructure | `ZiggyCreatures.FusionCache.Serialization.SystemTextJson` | `2.10.0` |
| Infrastructure | `Microsoft.Extensions.Caching.StackExchangeRedis` | `10.0.15` |
| Infrastructure | `Microsoft.Extensions.Diagnostics.HealthChecks` | `10.0.15` |
| Infrastructure | `Microsoft.Extensions.Options.DataAnnotations` | `10.0.15` |

`StackExchange.Redis` `3.4.35` llega con el backplane; no se referencia explícitamente salvo que la compilación lo exija.

### 7.2 Application

| Archivo | Acción |
|---|---|
| `src/Application/Common/Caching/ICacheInvalidator.cs` | Nuevo puerto. |
| `src/Application/Common/Caching/CacheKeys.cs` | Nuevo: `CacheKeys` + `CacheTags` + canonicalización/hash. |
| `src/Application/Common/Caching/CachePolicies.cs` | Nuevo: duraciones y fábrica de `FusionCacheEntryOptions` por clase. |
| `src/Application/Common/Caching/CachedResult.cs` | Nuevo sobre serializable + `CachedQueryResult<T>`. |
| `src/Application/Common/Caching/CacheBypassException.cs` | Nueva excepción interna de control de flujo. |
| `src/Application/Common/Caching/DirectoryCache.cs` | Nueva fachada de lectura/invalidación. |
| `src/Application/Common/DirectoryLdapHandlerBase.cs` | Nueva base con lectura cacheada e invalidación. |
| `src/Application/Common/LdapHandlerBase.cs` | Refactor menor: `PublishCompletedAsync` reutilizable. |
| 8 archivos `.../Queries/*/*Query.cs` | Heredan de la nueva base; constructor recibe `DirectoryCache`; `Handle` usa `ExecuteCachedQueryAsync`. |
| 4 archivos `.../Commands/*/*Command.cs` | Heredan de la nueva base; constructor recibe `DirectoryCache`; `Handle` usa `ExecuteMutationAsync`. |
| `src/Application/Common/Models/LdapDtos.cs` | `LdapEntryDto`: arrays/`IEnumerable` → `IReadOnlyList`. |
| `src/Application/Bitai.LDAPGateway.Application.csproj` | Referencia a FusionCache. |

Notas de implementación:

- La factory de caché no debe capturar servicios scoped más allá de la llamada; los handlers son scoped y la llamada es síncrona en su ciclo de vida.
- No se cachea en `IDirectoryServiceConnector` ni en `BitaiLdapHelperProvider` (se usan también en escrituras).

### 7.3 Infrastructure

| Archivo | Acción |
|---|---|
| `src/Infrastructure/Caching/CachingOptions.cs` | Nuevo: sección `Caching`, DataAnnotations + validación `MemoryCacheDuration <= Duration`. |
| `src/Infrastructure/Caching/CachingServiceCollectionExtensions.cs` | Nuevo `AddCaching(IServiceCollection, IConfiguration)`. |
| `src/Infrastructure/Caching/FusionCacheInvalidator.cs` | Nuevo. |
| `src/Infrastructure/Caching/GenerationTracker.cs` | Nuevo. |
| `src/Infrastructure/Caching/CacheReconciler.cs` | Nuevo. |
| `src/Infrastructure/Caching/CacheReconcilerHealthCheck.cs` | Nuevo. |
| `src/Infrastructure/Caching/LocalCacheInvalidator.cs` | Nuevo, para `Caching:Enabled=false` (invalida solo la L1 del nodo). |
| `src/Infrastructure/DependencyInjection.cs` | Llamar a `services.AddCaching(configuration)` al final. |
| `src/Infrastructure/Bitai.LDAPGateway.Infrastructure.csproj` | Paquetes de la sección 7.1. |

`AddCaching` con `Enabled=true`:

- Bind + validación de `CachingOptions` (`ValidateDataAnnotations().ValidateOnStart()`).
- Multiplexer singleton con la configuración de la sección 6.7.
- `AddStackExchangeRedisCache` + `RedisCacheOptions.ConnectionMultiplexerFactory`.
- `AddFusionCache()` con `CacheKeyPrefix`, `BackplaneChannelPrefix`, `DefaultEntryOptions` (`Duration = MemoryCacheDuration`, `DistributedCacheDuration = Duration`), serializer `FusionCacheSystemTextJsonSerializer` y backplane `RedisBackplane`.
- `ICacheInvalidator` → `FusionCacheInvalidator`.
- `CacheReconciler` singleton + `AddHostedService` + `AddHealthChecks().AddCheck<CacheReconcilerHealthCheck>("cache-reconciler", tags: ["ready"])`.

Con `Enabled=false`:

- `AddFusionCache()` sin L2, sin backplane y sin serializer.
- `ICacheInvalidator` → `LocalCacheInvalidator` (borra las entradas de la L1 del nodo; mantiene read-your-writes).
- Sin reconciliador ni comprobación de readiness de caché.
- No se valida la connection string.

### 7.4 Api

| Archivo | Acción |
|---|---|
| `src/Api/appsettings.json` | Sección `Caching` con prefijos, TTL, intervalos y timeouts; **sin** connection string. |
| `src/Api/appsettings.Testing.json` | `"Caching": { "Enabled": false }`. |
| `src/Api/appsettings.TestingStandard.json` | Entorno dedicado de los tests de seguridad: `Caching:Enabled=false`, bypass de scopes desactivado y un secreto de Swagger ficticio, para no depender de `appsettings.Development.json` (no versionado) ni de user-secrets. |
| `src/Api/Program.cs` | `app.MapHealthChecks("/health/ready", ...)` con `Predicate = c => c.Tags.Contains("ready")` y `/health/live` con `Predicate = _ => false`. |
| `src/Api/Internal/Generated/WolverineHandlers/*` | Regenerar con `dotnet run --project src/Api -- codegen write` y commitear. |

Ejemplo de configuración:

```json
"Caching": {
  "Enabled": true,
  "KeyPrefix": "bitai:ldapgateway:prod:v1:",
  "ChannelPrefix": "bitai:ldapgateway:prod",
  "Duration": "00:02:00",
  "MemoryCacheDuration": "00:00:30",
  "ReconcileInterval": "00:00:30",
  "MaxTrackedGenerations": 4096,
  "MaxEntriesToCache": 200,
  "ConnectTimeoutMs": 5000,
  "SyncTimeoutMs": 5000,
  "AsyncTimeoutMs": 5000,
  "KeepAliveSeconds": 60
}
```

La connection string se aporta por entorno:

```
Caching__RedisConnectionString=redis-host:6380,ssl=true,abortConnect=false
```

y en desarrollo con `dotnet user-secrets set "Caching:RedisConnectionString" "..." --project src/Api`.

## 8. Pruebas

### 8.1 Unitarias (Application.UnitTests)

- `CacheKeysTests`:
  - misma consulta → misma clave;
  - diferencias de mayúsculas/espacios en el valor → misma clave;
  - distinto `RequiredAttributeSet`, catálogo o perfil → claves distintas;
  - el valor del filtro no aparece en la clave;
  - longitud acotada.
- Cache hit: dos llamadas iguales ejecutan el conector una sola vez.
- Cache negativa: 404 o `null` se sirve desde caché dentro del TTL.
- Fallo 502: no se cachea; la segunda llamada vuelve a invocar el conector.
- Mutación exitosa: verifica `ICacheInvalidator.InvalidateAsync` con el tag del perfil y `CancellationToken.None`.
- Mutación fallida: no invalida.
- Fallo del invalidator: el comando sigue devolviendo éxito y se registra warning.
- `GetCatalogTypes`, perfiles y autenticación: no tocan `IFusionCache`.
- Búsqueda con más de `MaxEntriesToCache`: no se cachea.

Los tests usarán una instancia `new FusionCache(new FusionCacheOptions { CacheKeyPrefix = "test:" })` (solo L1), mocks de `IDirectoryServiceConnector` e `IDomainEventPublisher`, y un `ICacheInvalidator` falso que aplica realmente `RemoveAsync`/`RemoveByTagAsync` sobre ese FusionCache (así se prueba el read-after-write). Hay que actualizar `DirectoryTestSupport` y los constructores usados en `DirectoryQueryHandlersTests`, `DirectoryCommandHandlersTests` y `SetMsAdUserPasswordCommandTests`.

### 8.2 Funcionales (Api.FunctionalTests)

- `appsettings.Testing.json` deshabilita la caché para los tests normales.
- Los tests de seguridad usan un entorno dedicado `TestingStandard` (`appsettings.TestingStandard.json`), versionado: desactiva la caché, mantiene la seguridad de API activa y aporta un secreto ficticio para Swagger. Así no dependen de `appsettings.Development.json` (no versionado ni fiable) ni de user-secrets.
- Test de arranque: `/api/CatalogTypes` y `/api/ServerProfiles` siguen devolviendo 200.
- Test de health: `/health/live` y `/health/ready` responden 200 con caché deshabilitada.

**Nota de configuración:** `Caching:Enabled` se evalúa al registrar los servicios, cuando se ejecuta `WebApplication.CreateBuilder`. Por eso debe venir de `appsettings.{Environment}.json` o de variables de entorno; `IWebHostBuilder.ConfigureAppConfiguration` en un `WebApplicationFactory` se aplica demasiado tarde para esa decisión.

### 8.3 Comandos de validación

```
dotnet build Bitai.LDAPGateway.slnx
dotnet test Bitai.LDAPGateway.slnx
dotnet run --project src/Api -- codegen write
```

## 9. Secuencia de implementación

- [x] 1. Añadir paquetes a Application e Infrastructure.
- [x] 2. Crear `CachingOptions`, `AddCaching`, `LocalCacheInvalidator` e integrarlo en `AddInfrastructure`.
- [x] 3. Crear `ICacheInvalidator`, `CacheKeys`/`CacheTags`, `CachePolicies`, `CachedResult` y `CacheBypassException`.
- [x] 4. Crear `FusionCacheInvalidator`, `GenerationTracker`, `CacheReconciler` y `CacheReconcilerHealthCheck`.
- [x] 5. Crear `DirectoryCache`, `DirectoryLdapHandlerBase` y migrar los 8 queries y 4 comandos.
- [x] 6. Ajustar `LdapEntryDto` a colecciones de solo lectura.
- [x] 7. Configurar appsettings, Testing, TestingStandard y endpoints de health.
- [x] 8. Actualizar y ampliar tests.
- [x] 9. Regenerar código Wolverine y compilar.
- [x] 10. Ejecutar la suite y documentar en README.

### 9.1 Validación end-to-end realizada

Prueba de humo manual con un Redis local y el adaptador mock:

- Arranque correcto con serializer, `IDistributedCache` y backplane conectados; canales `bitai:ldapgateway:smoke.Backplane:v2` y `bitai:ldapgateway:smoke:cache:generation:events` con suscriptores.
- Primera búsqueda: miss de L1 y L2, factory LDAP, escritura en memoria y en Redis; TTL de L2 ≈ 2 min.
- Segunda búsqueda: hit de L1 (`Operation completed (cache hit)`); tras expirar L1, hit de L2.
- Mutación (`disableBy`): `INCR` de la generación a 1, escritura del marcador de tag en Redis y `PUBLISH` en el backplane.
- Búsqueda posterior: el marcador de tag es más nuevo que la entrada → factory de nuevo y respuesta actualizada (la entrada física de L2 permanece hasta su TTL, como es esperado en el modelo de tags de FusionCache).
- Claves físicas: FusionCache antepone su versión de protocolo (`v2:`), por ejemplo `v2:bitai:ldapgateway:smoke:v1:search-users:<hash>`; la clave de generación no lleva ese prefijo.

Toda la suite automatizada pasa: 1 test de dominio, 78 de Application y 32 funcionales.

## 10. Riesgos y límites aceptados

1. **Sin outbox**: invalidación best-effort; el TTL de L2 es la garantía para crashes y cambios externos.
2. **Crecimiento de Redis**: claves de búsqueda de alta cardinalidad; mitigado con TTL y `MaxEntriesToCache`.
3. **Carrera con factory en vuelo**: un lector que empezó antes de la invalidación puede repoblar con datos previos; se acepta (ventana de una ejecución de factory, acotada por TTL).
4. **Dos perfiles contra el mismo LDAP**: la invalidación de un perfil no limpia el otro; lo cubre el TTL.
5. **Seguridad**: PII en Redis; requiere TLS, ACL mínima (`SUBSCRIBE`/`PUBLISH` en el canal del backplane y de generación, `INCR`/`GET` en la clave de generación, comandos de L2 bajo `v2:<KeyPrefix>*`), credenciales fuera de `appsettings.json`.
6. **`maxmemory-policy`**: debe ser `volatile-*` o `noeviction` en el Redis del servicio para que la clave de generación no sea expulsable.
7. **Acoplamiento de código generado**: cualquier cambio de constructor en handlers requiere `codegen write`; no hacerlo rompe el arranque en Production.
8. **Preparación de readiness**: con Redis caído el pod queda `Unhealthy` hasta la primera suscripción; si el orquestador lo retira, el servicio deja de atender aunque podría leer sin caché. Aceptado por la decisión de topología.
9. **Arranque local**: con `Caching:Enabled=true`, `dotnet run` falla si no hay `Caching:RedisConnectionString`. Es intencional (fail-fast). Para trabajar sin Redis, exportar `Caching__Enabled=false` o configurar la connection string con user-secrets.

## 11. Fuera de alcance

- Redis Streams como fan-out durable (opción C del skill).
- Caché de autenticaciones o de decisiones de autorización.
- Caché distribuida de perfiles/catálogos.
- Testcontainers o pruebas multi-host automatizadas.
- Exportación de métricas a OpenTelemetry: se expone el meter, pero el exporter queda para el despliegue.
