# eslee Folder Locker

[⬇️ **Download the latest release**](https://github.com/esleeeeee/eslee-folder-locker/releases/latest)

**Document language:** [한국어](README.md) · English

`eslee Folder Locker` is a personal Windows folder-locking utility for local NTFS folders. It is designed for situations where you want to keep a folder from being casually opened in File Explorer without manually editing Windows permissions every time.

This is not a file encryption product. Files stay in their original location, and the app does not read, compress, move, rewrite, or inspect file contents. Instead, it changes Windows NTFS permissions so normal user-context access is denied.

The internal project and engine name remains `FolderGate` for compatibility. The public Korean product name is `eslee폴더잠금기`.

This project was implemented entirely through vibe coding. Product behavior, UI flow, NTFS ACL handling, recovery tooling, tests, release automation, and documentation were iterated through natural-language collaboration with an AI coding agent.

## When Would You Use This?

The app is intended for personal Windows PCs where you want a lightweight local access barrier.

Example use cases:

- Temporarily block casual access to a private work folder
- Reduce accidental browsing or modification through File Explorer
- Avoid full encryption when you only need a simple local access restriction
- Unlock the folder later with a password

This is not a strong security boundary. Administrators and users who understand Windows permissions can bypass or reverse it. For sensitive data, use Windows account separation, BitLocker, per-file encryption, or a dedicated security product.

## Basic Usage

1. Download the English zip package from the release page.
2. Extract it to a location you can remember.
3. Run `eslee-folder-locker.exe`.
4. Add the folder you want to lock.
5. Set a password the first time you use it.
6. Apply Quick mode or Hardened mode.
7. Unlock from the app or from the File Explorer right-click menu.

Windows may require the .NET 8 Desktop Runtime if it is not already installed.

## Download Files

Release packages are split by language.

- Korean: `eslee-folder-locker-vX.Y.Z-ko-win-x64.zip`
- English: `eslee-folder-locker-vX.Y.Z-en-win-x64.zip`

Main executables in the English package:

```text
eslee-folder-locker.exe
eslee-folder-locker-helper.exe
eslee-folder-locker-recovery.exe
```

Most users only need to run `eslee-folder-locker.exe`. The helper and recovery tool are used when locking, unlocking, or restoring permissions.

## How Folder Locking Works

`eslee Folder Locker` uses Windows NTFS ACLs. ACLs are how Windows controls access to files and folders.

The simplified flow is:

```text
Back up current permissions
        ↓
Identify the current Windows user SID
        ↓
Add a deny permission to the target folder
        ↓
On unlock, remove the app-added rule or restore the backed-up ACL
```

While locked, normal user-context operations are expected to fail:

- Opening the folder
- Listing folder contents
- Reading files
- Writing files
- Creating new files
- Creating child folders
- Deleting files
- Renaming files
- Copying external files into the locked folder

The original permissions are saved as JSON ACL backups. The recovery tool uses those backups to restore the original ACL state.

## Quick Mode And Hardened Mode

| Mode | What it does | Recommended for |
| --- | --- | --- |
| Quick mode | Applies the lock only to the selected folder root. | Blocking ordinary File Explorer entry quickly |
| Hardened mode | Recursively processes child folders and files, backing up and changing each ACL. | Stronger blocking across existing child items |

Hardened mode can take longer when a folder contains many items. It does not launch `icacls`, PowerShell, or `cmd.exe` once per file. A single elevated helper process uses .NET file enumeration APIs and Windows ACL APIs directly.

## Unlock From File Explorer

The app can register a File Explorer right-click unlock command.

On Windows 11, the command may appear under `Show more options`. `Shift + right-click` opens the expanded context menu directly.

English menu text:

```text
Unlock with eslee Folder Locker
```

After entering the correct password, you can choose how long the folder should stay unlocked:

- 1 minute
- 5 minutes
- 10 minutes
- 30 minutes
- 1 hour
- 1 day
- Permanent unlock

Temporary unlock stores an absolute UTC expiration time. If the PC is turned off before the selected duration expires, the app attempts to relock after the next Windows login. If the expiration time already passed while the PC was off, it attempts to relock immediately.

## What If I Delete The Program Folder?

Do not delete the program folder while folders are still locked.

The lock is not stored only inside the executable. Permission changes remain on the Windows file system, and the `data` folder beside the executables contains configuration, operation state, and ACL backups needed for unlock and recovery.

If you deleted the program while folders were locked:

1. Restore the deleted program folder from the Recycle Bin if possible.
2. If that is not possible, download the same or a newer release from GitHub.
3. Put any preserved `data` folder back beside the executables.
4. Run `eslee-folder-locker-recovery.exe` as administrator.
5. Select the ACL backup to restore.

If the `data` folder and ACL backups were also deleted, the app cannot reconstruct the original permissions automatically. A Windows administrator must manually inspect the folder permissions and remove the deny rules or repair the ACL.

## Paths The App Blocks

To reduce the chance of locking system paths or making recovery difficult, the app refuses risky targets:

- Drive roots
- Windows system folders
- Program Files
- ProgramData
- User profile root
- OneDrive root
- This project folder and its parent paths

For example, paths like `C:\`, `D:\`, `C:\Windows`, or the entire user profile should not be locked.

## Tested Behavior

Integration tests are designed to use temporary folders under `tests`, not real user folders.

Covered behavior includes:

- Locked folders deny opening, listing, reading, writing, creating, deleting, renaming, and copying
- Unlock restores the exact original ACL SDDL
- Cancellation and errors roll back already changed items in reverse order
- RecoveryTool can restore ACL backups from a separate process
- Hardened mode handles 10,000+ items without per-item external process launches
- UTC backup timestamps are shown to users in local time
- Password validation and WPF dialog layout checks

## Build From Source

You need the .NET 8 SDK.

```powershell
git clone https://github.com/esleeeeee/eslee-folder-locker.git
Set-Location eslee-folder-locker
dotnet restore .\FolderGate.sln
dotnet build .\FolderGate.sln
```

Build the Korean UI:

```powershell
dotnet build .\FolderGate.sln -p:AppLanguage=ko
```

Build the English UI:

```powershell
dotnet build .\FolderGate.sln -p:AppLanguage=en
```

Standard tests:

```powershell
dotnet test .\FolderGate.sln --filter "TestCategory!=RequiresElevation"
```

Elevation-required recovery tests:

```powershell
dotnet test .\tests\FolderGate.IntegrationTests\FolderGate.IntegrationTests.csproj --filter "TestCategory=RequiresElevation"
```

The elevation-required tests must be run from an elevated terminal.

## Project Layout

```text
src/
  FolderGate.App/             WPF desktop app
  FolderGate.Core/            Models, validation, password, ACL, and storage logic
  FolderGate.ElevatedHelper/  Performs actual ACL work after UAC elevation
  FolderGate.RecoveryTool/    Standalone ACL recovery tool

tests/
  FolderGate.App.Tests/
  FolderGate.Core.Tests/
  FolderGate.IntegrationTests/

assets/icons/
  App icon source and Windows ICO

tools/
  Icon generation script
```

## Technology

- C#
- .NET 8
- WPF
- Windows NTFS ACL
- `System.Security.AccessControl`
- PBKDF2-SHA256
- JSON / JSON Lines
- MSTest
- GitHub Actions

## Current Scope And Security Notes

This utility provides lightweight local access control for a personal Windows PC.

It cannot stop:

- Administrators
- Users who can take ownership or edit ACLs
- Offline disk access
- Malware
- Forensic tools
- Backup operator privileges

Do not rely on this app as the only protection for sensitive data. Use BitLocker, Windows account separation, or a dedicated encryption tool when you need stronger security.

## License

MIT License
