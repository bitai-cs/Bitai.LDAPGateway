# Plan: Configuración independiente de niveles de caché (L1 / L2 / ambos / ninguno)

- **Fecha:** 2026-11-27
- **Estado:** Plan aprobado, pendiente de implementación.
- **Reemplaza** el borrador anterior de este archivo, que concluía que no hacía falta un flag L1.

## 1. Contexto

Hoy `Caching:Enabled` solo conmuta L2 (Redis) + backplane + reconciler; L1 (memoria) siempre existe. Se requiere operar, por configuración, en 4 modos: **solo L1**, **solo L2**, **L1+L2** y **sin caché**.

## 2. Respuesta y factibilidad

**¿Pueden L1 y L2 trabajar independientemente? Sí, es factible**, con matices:

| Modo | Factibilidad | Cómo |
|---|---|---|
| L1 + L2 | Ya existe | `FusionCacheInvalidator` + backplane + `CacheReconciler` |
| Solo L1 | Ya existe (`Enabled=false`) | `LocalCacheInvalidator` |
| Solo L2 | Factible (FusionCache 2.10.0 soporta `SkipMemoryCacheRead/Write`) | Sin backplane ni reconciler (no hay L1 que reconciliar); invalidación por `RemoveByTagAsync` contra L2. **Riesgo a validar con spike**: que `RemoveByTag` sea efectivo con L1 omitido |
| Sin caché | Factible | Bypass en `DirectoryCache` (llama a la operación directamente) + `NoOpCacheInvalidator` |

FusionCache no tiene un "apagar L1" global; se logra con flags por entrada (`SkipMemoryCache*`), aplicados centralmente en `DirectoryCache`.

## 3. Diseño

### Opciones (`src/Infrastructure/Caching/CachingOptions.cs`)
- `MemoryCacheEnabled` (bool, default `true`) → L1.
- `DistributedCacheEnabled` (bool, default `true`) → L2 + Redis (+ backplane/reconciler solo si L1 también está activo).
- `Enabled` se **elimina** (cambio incompatible, aceptado): sin alias ni warning. Los despliegues y configuraciones existentes deben actualizarse a las claves nuevas (`Caching__Enabled` → `Caching__DistributedCacheEnabled` / `Caching__MemoryCacheEnabled`).
- Validación: Redis/`KeyPrefix`/`ChannelPrefix` requeridos solo si `DistributedCacheEnabled`; `MemoryCacheDuration <= Duration` solo si ambos; backplane/`ReconcileInterval`/`MaxTrackedGenerations` solo aplican en modo L1+L2.
- Tipo `CacheMode { None, MemoryOnly, DistributedOnly, Hybrid }` resuelto desde las opciones y expuesto a Application mediante una abstracción pequeña (`ICacheLevels`/`CacheMode` en `Application/Common/Caching`) para no acoplar Application a Infrastructure.

### Registro DI (`CachingServiceCollectionExtensions.AddCaching`)
- `None`: `AddFusionCache()` mínimo (para no romper DI) + `NoOpCacheInvalidator`; sin Redis.
- `MemoryOnly`: ruta actual `AddDisabledCaching` (renombrar a `AddMemoryOnlyCaching`) + `LocalCacheInvalidator`.
- `DistributedOnly` (**requisito validado por el spike**): configurar `FusionCacheOptions.TagsDefaultEntryOptions` con `SkipMemoryCacheRead/Write = true`. Sin esto, el nodo B guarda en memoria los metadatos del tag y sigue sirviendo datos viejos tras un `RemoveByTag` en el nodo A (no hay backplane). Además: multiplexer + `AddStackExchangeRedisCache` + serializer + `WithRegisteredDistributedCache()`, **sin** `WithBackplane`, sin `CacheReconciler`/health check; nuevo `DistributedCacheInvalidator` (Remove/RemoveByTag, sin INCR de generación ni pub/sub).
- `Hybrid`: sin cambios.

### `DirectoryCache` (`src/Application/Common/Caching/DirectoryCache.cs`)
Recibe el modo; en `GetOrSetAsync`:
- `None` → ejecutar `operation` y devolver sin tocar caché (conservar semántica de errores y `servedFromCache=false`).
- `DistributedOnly` → clonar `entryOptions` con `SkipMemoryCacheRead/Write = true`.
- `MemoryOnly` → `SkipDistributedCacheRead/Write = true` (defensivo; no hay L2 registrado).
- `allowDistributedCache` / `MaxEntriesToCache`: en `DistributedOnly` un resultado grande no se cachea en ningún nivel.

### Reglas de capas (Clean Architecture)
Verificado contra las referencias actuales: `Infrastructure → Application → Domain`; Application no referencia Infrastructure y ya depende del paquete base `ZiggyCreatures.FusionCache`.
- **Application:** solo conoce `CacheMode`/`ICacheLevels` y traduce el modo a flags de entrada de FusionCache (`SkipMemoryCache*`, `SkipDistributedCache*`) o a bypass. No referencia Redis, backplane ni `CachingOptions`.
- **Infrastructure:** `CachingOptions`, resolución del modo desde la configuración, registro DI, Redis, backplane, reconciliador y todos los `ICacheInvalidator` (implementan la interfaz definida en Application).
- Mantener `IFusionCache` registrado en modo `None` es una concesión consciente; abstraer el caché detrás de una interfaz propia queda fuera de alcance (refactor mayor sin beneficio para este cambio).

## 4. Archivos
- **Modificar:** `CachingOptions.cs`, `CachingServiceCollectionExtensions.cs`, `DirectoryCache.cs`, `Application/DependencyInjection.cs` (si hace falta registrar el modo), `CachePolicies.cs` (comentarios).
- **Nuevos:** `Infrastructure/Caching/DistributedCacheInvalidator.cs`, `Infrastructure/Caching/NoOpCacheInvalidator.cs`, `CacheMode` + abstracción en Application.
- **Config:** `src/Api/appsettings.json`, `appsettings.Development.json`, `appsettings.Testing.json`, `appsettings.TestingStandard.json` (usar `MemoryCacheEnabled` / `DistributedCacheEnabled`; en tests `DistributedCacheEnabled=false`).
- **Docs:** `README.md` sección Caching (matriz de modos, variables `Caching__MemoryCacheEnabled` y `Caching__DistributedCacheEnabled`, nota de que `Caching__Enabled` fue eliminada).

## 5. Pasos
1. ✅ **Spike completado** (`tests/Application.UnitTests/DistributedOnlySpikeTests.cs`, 3 tests en verde): L2-only sirve desde L2; `RemoveByTag` funciona en un nodo; entre dos nodos que comparten L2 **solo** funciona con `TagsDefaultEntryOptions` omitiendo memoria (sin ello falló). Pendiente de confirmar con Redis real en la verificación manual. Spike original: FusionCache + `MemoryDistributedCache` + serializer con `SkipMemoryCache*` y `RemoveByTagAsync` → confirmar read-your-writes tras invalidar (valida el riesgo de L2-only).
2. Opciones + `CacheMode` + validaciones (eliminar `Enabled`).
3. DI por modo + nuevos invalidadores.
4. `DirectoryCache` por modo.
5. Config y README.
6. Tests.

## 6. Verificación
- **Unit** (`tests/Application.UnitTests/DirectoryCachingTests.cs`, ampliar): por cada modo (None, L1, L2 con `MemoryDistributedCache`, Hybrid): 2ª llamada = hit/miss esperado; tras `InvalidateProfileAsync` la lectura siguiente refleja el cambio; `None` nunca cachea; los errores de infraestructura no se cachean.
- **Opciones:** matriz de validación (Redis requerido solo con L2; `Duration` ≥ `MemoryCacheDuration` solo con ambos).
- **DI:** cada modo resuelve el `ICacheInvalidator` correcto y registra/omite `CacheReconciler`, `IConnectionMultiplexer` y el health check.
- `dotnet test` de la solución (Api.FunctionalTests con `DistributedCacheEnabled=false`). Prueba manual opcional con Redis local para L2-only y L1+L2 con dos instancias.

## 7. Decisiones asumidas
- `Caching:Enabled` se elimina sin compatibilidad hacia atrás (confirmado por el usuario: los despliegues existentes deben actualizarse).
- En `DistributedOnly` no hay backplane/reconciler; la coherencia multi-nodo viene de Redis como única fuente.
- Nombres de claves: `MemoryCacheEnabled` / `DistributedCacheEnabled`.

## 8. Estado de ejecución
- ✅ Pasos 1–6 implementados. Suite completa en verde (Application 91, Domain 1, Api.Functional 33, Infrastructure 22 con 2 omitidos sin Redis).
- ✅ **Verificación con Redis real** (`tests/Infrastructure.UnitTests/CachingRedisIntegrationTests.cs`, opt-in con la variable `BITAI_IT_REDIS`): dos nodos con el DI real de `AddCaching` sobre el mismo Redis. Solo L2: comparten entradas y la invalidación llega al otro nodo de inmediato. L1+L2: comparten entradas y el backplane invalida la L1 del otro nodo (espera acotada de 5 s).
- Pendiente fuera de alcance: abstraer el caché detrás de una interfaz propia (ver "Reglas de capas").

