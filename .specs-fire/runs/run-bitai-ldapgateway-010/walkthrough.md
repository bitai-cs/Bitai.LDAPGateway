# Walkthrough: run-bitai-ldapgateway-010

Added boundary tests for the 12 use cases that had none (Create/SetPassword/Disable/Delete, three searches, three single-entry lookups, two parents queries), including their validators. Shared fakes live in `DirectoryTestSupport`. Run with `dotnet test tests/Application.UnitTests`.
