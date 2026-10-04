# Code generation and production

Read when: preparing a Wolverine host for production, running `codegen write`, or debugging startup errors about code generation.

Wolverine generates the handler pipeline code. Source: https://wolverinefx.net/guide/codegen.

- **Development (Dynamic, the default).** Handlers are compiled at runtime. From Wolverine 6.0 the runtime compiler is a separate package: with only `WolverineFx` the host fails at startup with `InvalidOperationException ... no IAssemblyGenerator (Roslyn) is registered` (verified in a new host). Reference `WolverineFx.RuntimeCompilation` (same version as `WolverineFx`) in the host. The startup message `The Wolverine code generation mode is Dynamic` is informational.
- **Production (Static, recommended by the docs).** Pre-generate the code and load it from the host assembly:

```csharp
opts.Services.CritterStackDefaults(x =>
{
    x.Production.GeneratedCodeMode = TypeLoadMode.Static;       // using JasperFx.CodeGeneration;
    x.Production.AssertAllPreGeneratedTypesExist = true;
});
```

- End `Program.cs` with `return await app.RunJasperFxCommands(args);` (`using JasperFx;`) instead of `app.Run();`, so `dotnet run -- codegen write` exists. Per the docs, `codegen write` blocks or fails if `Program.cs` reaches a database or broker before that line; defer infrastructure setup to hosted services.
- `dotnet run --project <host> -- codegen write` (Debug build, where RuntimeCompilation is present) writes `Internal/Generated/WolverineHandlers` in the host project. The docs say to commit those files, and to delete the existing ones when handler signatures change or middleware is added or removed.
- Make the runtime compiler Debug-only so Release has no Roslyn (the docs mention about 100 MB): `<PackageReference Include="WolverineFx.RuntimeCompilation" Version="..." Condition="'$(Configuration)' == 'Debug'" />`. Verified: the Release output had no Roslyn or RuntimeCompilation DLLs and, started with `ASPNETCORE_ENVIRONMENT=Production`, logged `code generation mode is Static with pre-generated types` and served requests.
- **Stale generated code (verified).** Deleting a generated handler file breaks the build (`GeneratedHandlerRegistry.cs` references every handler type). Adding a handler without regenerating is NOT caught by `AssertAllPreGeneratedTypesExist`: the host boots (the registry log says it is skipping assembly scan) and the new message fails at runtime with `IndeterminateRoutesException`. In CI run `codegen write` and fail if `Internal/Generated` changes (`git diff --exit-code` plus an untracked-files check); regenerating twice produced no diff locally, but this gate was not run in a CI pipeline. Do not use `Auto` in production (docs).
- `WebApplicationFactory<Program>` tests need `public partial class Program;` and, in Debug, the RuntimeCompilation package in the host.
