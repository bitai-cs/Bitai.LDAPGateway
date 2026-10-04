# Testing best practices (C# / .NET)

Read when: writing, changing or reviewing tests, or running them and measuring coverage.

## Test structure

- Separate test project named `[ProjectName].Tests`.
- Mirror classes: `CatDoor` -> `CatDoorTests`.
- Name tests by behavior: `WhenCatMeowsThenCatDoorOpens`. Follow existing naming conventions first.
- Use public instance classes; avoid static fields.
- No branching or conditionals inside tests.

## Unit tests

- One behavior per test, following Arrange-Act-Assert.
- Use clear assertions that verify the outcome expressed by the test name; assert specific values and edge cases, not vague outcomes.
- Avoid multiple assertions in one test; prefer multiple tests.
- Several preconditions: one test for each. Several outcomes for one precondition: a parameterized test.
- Tests must run in any order and in parallel.
- Avoid disk I/O; if needed, randomize paths, do not clean up, and log file locations.
- Test through public APIs; do not change visibility and avoid `InternalsVisibleTo`.
- Require tests for new or changed public APIs.
- Avoid Unicode symbols.

## Test workflow

- Look for custom targets and scripts: `Directory.Build.targets`, `test.ps1`/`.cmd`/`.sh`.
- .NET Framework: `vstest.console.exe` directly, or Visual Studio Test Explorer.
- Work on one test until it passes, then run the other tests to make sure nothing broke.

### Code coverage (dotnet-coverage)

Install once:

```bash
dotnet tool install -g dotnet-coverage
```

Run locally every time tests are added or modified:

```bash
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

## Framework-specific guidance

Use the framework already in the solution (xUnit, NUnit or MSTest) for new tests.

### xUnit

- Packages: `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`.
- No class attribute; use `[Fact]`. Parameterized: `[Theory]` with `[InlineData]`.
- Setup and teardown: constructor and `IDisposable`.

### xUnit v3

- Packages: `xunit.v3`, `xunit.runner.visualstudio` 3.x, `Microsoft.NET.Test.Sdk`.
- `ITestOutputHelper` and `[Theory]` are in the `Xunit` namespace.

### NUnit

- Packages: `Microsoft.NET.Test.Sdk`, `NUnit`, `NUnit3TestAdapter`.
- Class `[TestFixture]`, test `[Test]`. Parameterized: `[TestCase]`.

### MSTest

- Class `[TestClass]`, test `[TestMethod]`. Setup and teardown: `[TestInitialize]`, `[TestCleanup]`.
- Parameterized: `[TestMethod]` with `[DataRow]`.

### Assertions

- If FluentAssertions or AwesomeAssertions are already used, prefer them; otherwise use the framework's asserts.
- Use `Throws`/`ThrowsAsync` (or MSTest `Assert.ThrowsException`) for exceptions.

## Mocking

- Avoid mocks and fakes if possible.
- External dependencies can be mocked. Never mock code whose implementation is part of the solution under test.
- Verify that the outputs of a mock (return values, exceptions) match the real dependency. A test for this can be written and left skipped or explicit so developers can run it later.
