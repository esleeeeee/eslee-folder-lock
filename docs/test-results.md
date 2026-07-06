# Test Validation

This project includes unit tests, WPF layout tests, and Windows ACL integration tests.

The repository intentionally does not include local runtime data, logs, ACL backups, or temporary test runs.

## Standard Validation

Run from the repository root:

```powershell
dotnet restore .\FolderGate.sln
dotnet build .\FolderGate.sln
dotnet test .\FolderGate.sln --filter "TestCategory!=RequiresElevation"
```

The `RequiresElevation` tests are excluded from the standard command because they require an elevated Windows terminal.

## Elevated Validation

To verify the administrator-only RecoveryTool process path, start an elevated terminal and run:

```powershell
dotnet test .\tests\FolderGate.IntegrationTests\FolderGate.IntegrationTests.csproj --filter "TestCategory=RequiresElevation"
```

## Covered Behavior

- Password validation and password dialog layout.
- Korean app display name and WPF icon resource loading.
- NTFS target path validation.
- ACL backup save/load behavior.
- UTC storage with local-time display for user-facing backup and log timestamps.
- Hardened-mode ACL lock behavior in temporary folders under `tests`.
- Unlock restoring the original ACL SDDL.
- Cancellation rollback in reverse changed-item order.
- Simulated ACL failure counting and rollback.
- Large-folder hardened-mode processing without per-item external process launches.
- RecoveryTool restore through a separate process when run from an elevated test session.

## Data Safety

Integration tests create temporary folders under `tests/FolderGate.IntegrationTests/TestRuns/<guid>` and clean them up after execution.

The tests are designed not to modify real user folders or fixed personal paths.
