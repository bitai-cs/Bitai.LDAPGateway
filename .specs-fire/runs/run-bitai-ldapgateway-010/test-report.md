# Test Report: run-bitai-ldapgateway-010

New files in `tests/Application.UnitTests/`: `DirectoryTestSupport.cs`, `DirectoryCommandHandlersTests.cs`, `DirectoryQueryHandlersTests.cs` (38 new test cases).

Coverage per handler (Moq connector and publisher, no directory):
- CreateMsAdUser, SetMsAdUserPassword, DisableMsAdUser, DeleteMsAdUser: success forwards exact context and arguments and publishes one `OperationCompletedDomainEvent`; connector failure is returned unchanged and published as failed.
- SearchDirectory, SearchUsers, SearchGroups: same success/failure pair; validators reject blank filter value, a lone second-filter attribute and invalid enums.
- GetUserByIdentifier, GetGroupByIdentifier, GetDirectoryEntryByIdentifier: found; not found with MustExist true gives `not_found`, with false gives success and a null value.
- GetUserParents, GetGroupParents: forward the MustExist flag; failure published as failed.
- Write-command validators reject blank profile / identifier / password.
- SetMsAdUserPassword failure: the published event never contains the password.

Results: Application 62 (was 24), Functional 30, Domain 1, all pass. Release build succeeds with no warnings from this repo.
