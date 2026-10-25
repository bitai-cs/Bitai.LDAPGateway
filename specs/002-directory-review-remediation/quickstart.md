# Quickstart: Directory Review Remediation

## Prerequisites

- .NET SDK 10.0.402 or compatible SDK installed.
- Dependencies restored for the repository.
- Work from the repository root.

## Validate the Application Boundaries

Run the focused application test project:

```powershell
dotnet test tests\Application.UnitTests\Bitai.LDAPGateway.Application.UnitTests.csproj
```

Expected outcome:

- The zero-valued defined primary directory attribute is accepted.
- Undefined primary attributes and all partial secondary-filter combinations are
  rejected.
- Simulated group and directory-entry connector failures preserve their error and
  produce one failed completion event.
- Successful Active Directory user provisioning forwards the request, returns the
  connector's entry, and produces one matching completion event.
- Extra or mismatched completion events cause the relevant test to fail.

## Validate Build Integrity

Run the release build:

```powershell
dotnet build Bitai.LDAPGateway.slnx -c Release
```

Expected outcome: the solution builds without warnings or errors.

## Contract Reference

Review [directory-validation.md](./contracts/directory-validation.md) for the
externally observable search-validation behavior and
[data-model.md](./data-model.md) for request and completion-record invariants.
