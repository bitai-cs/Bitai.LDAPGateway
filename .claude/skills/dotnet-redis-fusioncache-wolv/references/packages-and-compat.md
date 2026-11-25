**Compatibility matrix** (record the real versions in the target solution; do not copy this table as a statement of tested versions):

| Component | Expected | Notes |
|---|---|---|
| Target framework | .NET 10 (`net10.0`) | [Assumption] |
| `ZiggyCreatures.FusionCache` | 2.x | Tagging (`RemoveByTag`), `Clear`, and the backplane API used below are 2.x features. |
| `ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis` | same version as the core package | Keep all FusionCache packages on the same version. |
| `ZiggyCreatures.FusionCache.Serialization.SystemTextJson` (or another serializer) | same version | A serializer is required as soon as L2 is configured. |
| `Microsoft.Extensions.Caching.StackExchangeRedis` / `StackExchange.Redis` | 10.x / compatible | L2 as `IDistributedCache`. One shared `IConnectionMultiplexer`. |
| `WolverineFx` + persistence package + `WolverineFx.RuntimeCompilation` | version already used by the solution | Only for the outbox/handler pattern. Wolverine 6.x refuses to start without the runtime compiler package (or pre-generated code), see Step 1. |
| Redis | any version with Pub/Sub and `INCR` | The deployment's `maxmemory-policy` MUST NOT be able to evict the generation key (Step 3e). |


## Step 1: Packages

Install packages **per project**.

```bash
# Application project
dotnet add package ZiggyCreatures.FusionCache

# Infrastructure project
dotnet add package ZiggyCreatures.FusionCache
dotnet add package ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis
dotnet add package ZiggyCreatures.FusionCache.Serialization.SystemTextJson
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
dotnet add package StackExchange.Redis
# Compiled by AddCaching/AddCheck inside Infrastructure; the host's shared
# framework reference does not flow into a class library:
dotnet add package Microsoft.Extensions.Options.DataAnnotations
dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks
```

Keep every `ZiggyCreatures.FusionCache*` package on the same version. Use the 10.x versions of the Microsoft packages for .NET 10. Wolverine packages are already present in the target solution (`WolverineFx`, plus the persistence package, e.g. `WolverineFx.EntityFrameworkCore` and `WolverineFx.SqlServer` / `WolverineFx.Postgresql`). Wolverine 6.x no longer ships the runtime compiler in the core package: without `WolverineFx.RuntimeCompilation` (or pre-generated code with `TypeLoadMode.Static`) the host fails at startup with "no IAssemblyGenerator (Roslyn) is registered". [Verified] with WolverineFx 6.64.2. Re-check the package split and the Wolverine APIs against the versions you pin.

The snippets omit most `using` directives. Typical namespaces: `ZiggyCreatures.Caching.Fusion`, `ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis`, `ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson`, `Microsoft.Extensions.Caching.StackExchangeRedis`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging` (the `LogWarning`/`LogError` extensions), `Microsoft.Extensions.Diagnostics.HealthChecks`, `StackExchange.Redis`, `System.Diagnostics.Metrics`. For the Wolverine snippets: `Wolverine`, `Wolverine.EntityFrameworkCore`, `Wolverine.SqlServer` (or `Wolverine.Postgresql`), `Wolverine.ErrorHandling` (the `OnException` extension) and `JasperFx.Core` (`50.Milliseconds()`).
