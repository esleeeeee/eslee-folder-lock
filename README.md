# eslee folder lock

eslee folder lock is a Windows 11 desktop utility for restricting casual access to selected folders by changing NTFS permissions. It is built on the internal FolderGate engine and is designed for local, personal use cases where the goal is to prevent ordinary File Explorer access rather than provide cryptographic data protection.

This project was implemented entirely through vibe coding: the product direction, behavior, UI, tests, recovery flow, release packaging, and documentation were iterated through natural-language collaboration with an AI coding agent.

## What It Does

eslee folder lock lets a user register local NTFS folders and apply an access-deny lock to them. Once locked, a normal user session should fail common file operations such as opening the folder, listing contents, reading files, writing files, creating new files or directories, renaming files, deleting files, and copying external files into the locked target.

The application is intended for practical local access control. It does not encrypt files, hide files at the kernel level, or transform file contents. The folder and file data remain on disk exactly where they are.

## How Folder Protection Works

The lock is implemented with Windows NTFS Access Control Lists, not with encryption or background file rewriting.

- The app records the original ACL state before applying a lock.
- It adds deny rules for the target user SID.
- Unlock and recovery paths remove the app-owned deny rules or restore the saved ACL backup.
- Backup data is stored as JSON under the application data directory.
- User-facing timestamps are shown in local time while persisted technical timestamps remain UTC.

The project supports two lock modes:

- Quick mode: applies the lock to the selected folder root. This is fast and useful for blocking normal Explorer entry into a folder.
- Hardened mode: recursively enumerates child directories and files, backs up ACLs, and applies ACL changes to each item.

Hardened mode is intentionally implemented inside one elevated helper process. It does not spawn `icacls`, PowerShell, or `cmd.exe` once per file. The helper uses .NET file-system enumeration APIs and Windows ACL APIs directly, which keeps large-folder processing more predictable.

## Recovery Model

NTFS permissions persist independently of this application. Deleting the program does not automatically unlock folders.

For that reason, the project includes a standalone recovery tool:

- `이은성폴더잠금기_복구도구.exe`
- Internal project name: `FolderGate.RecoveryTool.exe`

The recovery tool runs elevated, reads ACL backup files, lets the user select a backup, and restores the original permissions after explicit confirmation.

## Important Security Notes

This is not an encryption product.

Do not treat this tool as protection against administrators, forensic tools, offline disk access, malware, or users who understand NTFS permissions. A Windows administrator or a sufficiently technical user can inspect, modify, bypass, or repair ACLs manually.

This project is meant to reduce accidental access and casual browsing through File Explorer. It is not a substitute for full-disk encryption, per-file encryption, account isolation, device management policy, or professional endpoint security.

## Paths The App Refuses To Lock

The application rejects high-risk targets by default, including:

- Drive roots
- Windows system folders
- `Program Files`, `Program Files (x86)`, and `ProgramData`
- User profile roots
- OneDrive roots
- The project root and parent directories of the project root

This is intentional. Locking broad system paths can break Windows, block recovery tools, or make the machine difficult to repair.

## Technology Stack

- Language: C#
- Runtime: .NET 8
- UI: WPF
- Platform: Windows 11
- File-system security: `System.Security.AccessControl`
- Password hashing: PBKDF2-SHA256
- Persistence: JSON configuration, JSON ACL backups, JSON Lines operation logs
- Testing: MSTest
- Release automation: GitHub Actions

## Project Structure

```text
src/
  FolderGate.App/             WPF desktop application
  FolderGate.Core/            Models, validation, password hashing, ACL logic, storage
  FolderGate.ElevatedHelper/  Elevated lock/unlock/restore helper
  FolderGate.RecoveryTool/    Standalone ACL recovery console tool

tests/
  FolderGate.App.Tests/
  FolderGate.Core.Tests/
  FolderGate.IntegrationTests/
  manual-smoke/

assets/icons/
  Windows application icon source and generated sizes

tools/
  Icon generation script
```

The public English product name is eslee folder lock. The Korean product name is 이은성폴더잠금기. Internal project names and namespaces still use `FolderGate` for compatibility.

## Build

With the .NET 8 SDK installed:

```powershell
dotnet restore .\FolderGate.sln
dotnet build .\FolderGate.sln
```

The repository also contains a local SDK bootstrap script under `build/` for environments where the SDK is not already installed.

## Test

Standard validation:

```powershell
dotnet test .\FolderGate.sln --filter "TestCategory!=RequiresElevation"
```

Some integration tests require an elevated Windows terminal because they verify administrator-only restore behavior:

```powershell
dotnet test .\tests\FolderGate.IntegrationTests\FolderGate.IntegrationTests.csproj --filter "TestCategory=RequiresElevation"
```

Integration tests are designed to operate only under temporary test folders inside the repository. They should not modify real user folders or fixed personal paths.

## Release

Published releases are built by GitHub Actions from a clean checkout. Runtime state is not included.

Excluded from releases and source commits:

- `data/`
- Local configuration
- Operation logs
- ACL backups
- Test run folders
- `bin/` and `obj/`
- Local `release/` output
- Local SDK cache under `build/.dotnet/`

Download the latest ZIP from GitHub Releases, extract it, and run:

```text
이은성폴더잠금기.exe
```

Windows may require the .NET 8 Desktop Runtime if it is not already installed.

## Icon Generation

The application icon is generated from the root PNG source and stored under `assets/icons`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Generate-EsleeFolderLockIcon.ps1
```

Generated outputs include PNG sizes from 16px through 256px and a multi-size Windows `.ico` file.

---

# 이은성폴더잠금기

이은성폴더잠금기는 Windows 11에서 특정 폴더의 일반적인 탐색기 접근을 제한하기 위한 개인용 데스크톱 도구입니다. 내부 구현은 FolderGate 엔진을 기반으로 하며, 파일을 암호화하는 프로그램이 아니라 NTFS 권한을 조정해 폴더 접근을 차단하는 방식으로 동작합니다.

이 프로젝트는 전부 바이브코딩으로 구현되었습니다. 제품 방향, 기능 정의, UI, ACL 처리 로직, 복구 도구, 테스트, 릴리스 자동화, 문서화까지 자연어 기반 협업으로 반복 구현했습니다.

## 프로그램의 목적

이은성폴더잠금기는 사용자가 등록한 NTFS 폴더에 접근 거부 권한을 적용합니다. 잠금 상태에서는 일반 사용자 컨텍스트에서 폴더 열기, 목록 조회, 파일 읽기, 쓰기, 새 파일 생성, 새 하위 폴더 생성, 이름 변경, 삭제, 외부 파일 복사 같은 일반 작업이 실패하도록 설계되어 있습니다.

목표는 실사용 환경에서의 가벼운 로컬 접근 제한입니다. 파일 내용은 암호화하지 않고, 파일을 다른 위치로 옮기거나 다시 쓰지도 않습니다. 데이터는 원래 위치에 그대로 남아 있습니다.

## 폴더 보안 구현 방식

이 프로그램은 암호화 대신 Windows NTFS ACL을 사용합니다.

- 잠금 전 원래 ACL 상태를 백업합니다.
- 대상 사용자 SID에 대해 deny rule을 추가합니다.
- 잠금 해제 또는 복구 시 프로그램이 추가한 deny rule을 제거하거나 백업된 ACL을 복원합니다.
- ACL 백업은 JSON으로 저장합니다.
- 사용자에게 보이는 시간은 로컬 시간으로 표시하고, 내부 저장용 기술 timestamp는 UTC를 유지합니다.

잠금 방식은 두 가지입니다.

- 빠른 모드: 선택한 폴더 루트에만 잠금을 적용합니다. 탐색기에서 폴더를 여는 일반적인 접근을 빠르게 차단하는 용도입니다.
- 강화 모드: 하위 폴더와 파일을 재귀적으로 순회하면서 각 항목의 ACL을 백업하고 변경합니다.

강화 모드는 UAC로 승격된 도우미 프로세스 하나에서 처리합니다. 파일마다 `icacls`, PowerShell, `cmd.exe`를 새로 실행하지 않습니다. .NET의 파일 시스템 순회 API와 Windows ACL API를 직접 사용해 대량 항목 처리 시 불필요한 외부 프로세스 생성을 피합니다.

## 복구 구조

NTFS 권한은 프로그램 파일과 별개로 Windows 파일 시스템에 남습니다. 프로그램을 삭제했다고 해서 이미 잠긴 폴더가 자동으로 풀리지는 않습니다.

이를 위해 별도의 복구 도구를 제공합니다.

- `이은성폴더잠금기_복구도구.exe`
- 내부 프로젝트명: `FolderGate.RecoveryTool.exe`

복구 도구는 관리자 권한으로 실행되며, ACL 백업 파일을 읽고 사용자가 선택한 백업을 기준으로 원래 권한을 복원합니다. 복구 실행 전에는 명시적인 확인 입력을 요구합니다.

## 보안상 주의사항

이 프로그램은 암호화 제품이 아닙니다.

관리자 권한을 가진 사용자, NTFS 권한 구조를 이해하는 사용자, 오프라인 디스크 접근, 포렌식 도구, 악성코드까지 막는 보안 경계로 사용하면 안 됩니다. 관리자나 충분히 기술적인 사용자는 ACL을 직접 확인하거나 수정하거나 우회할 수 있습니다.

이 프로그램은 파일 탐색기를 통한 우발적 접근이나 가벼운 열람을 줄이는 도구입니다. 전체 디스크 암호화, 파일 단위 암호화, 계정 분리, 조직 보안 정책, 전문 보안 솔루션을 대체하지 않습니다.

## 잠금 대상에서 제외하는 경로

다음 경로는 기본적으로 잠금 대상으로 사용할 수 없게 막습니다.

- 드라이브 루트
- Windows 시스템 폴더
- `Program Files`, `Program Files (x86)`, `ProgramData`
- 사용자 프로필 루트
- OneDrive 루트
- 프로젝트 루트와 그 상위 경로

이 제한은 의도된 안전장치입니다. 시스템 경로나 너무 넓은 범위를 잠그면 Windows 동작, 복구 도구 접근, 사용자 데이터 복구가 어려워질 수 있습니다.

## 구현에 사용한 기술

- 언어: C#
- 런타임: .NET 8
- UI: WPF
- 대상 플랫폼: Windows 11
- 파일 시스템 권한 처리: `System.Security.AccessControl`
- 비밀번호 해싱: PBKDF2-SHA256
- 저장 방식: JSON 설정, JSON ACL 백업, JSON Lines 작업 로그
- 테스트: MSTest
- 릴리스 자동화: GitHub Actions

## 프로젝트 구조

```text
src/
  FolderGate.App/             WPF 데스크톱 앱
  FolderGate.Core/            모델, 검증, 비밀번호 해싱, ACL 로직, 저장소
  FolderGate.ElevatedHelper/  관리자 권한 잠금/해제/복구 도우미
  FolderGate.RecoveryTool/    독립 실행 ACL 복구 콘솔 도구

tests/
  FolderGate.App.Tests/
  FolderGate.Core.Tests/
  FolderGate.IntegrationTests/
  manual-smoke/

assets/icons/
  앱 아이콘 원본과 생성된 크기별 아이콘

tools/
  아이콘 생성 스크립트
```

사용자에게 보이는 제품명은 이은성폴더잠금기입니다. 내부 프로젝트명과 네임스페이스는 호환성을 위해 `FolderGate`를 유지합니다.

## 빌드 방법

.NET 8 SDK가 설치된 환경에서 실행합니다.

```powershell
dotnet restore .\FolderGate.sln
dotnet build .\FolderGate.sln
```

SDK가 설치되어 있지 않은 환경을 고려해 `build/` 아래에 로컬 SDK 설치 스크립트도 포함되어 있습니다.

## 테스트 방법

일반 검증:

```powershell
dotnet test .\FolderGate.sln --filter "TestCategory!=RequiresElevation"
```

관리자 권한이 필요한 복구 경로 테스트는 관리자 권한 터미널에서 별도로 실행합니다.

```powershell
dotnet test .\tests\FolderGate.IntegrationTests\FolderGate.IntegrationTests.csproj --filter "TestCategory=RequiresElevation"
```

통합 테스트는 저장소 내부의 임시 테스트 폴더에서만 동작하도록 설계되어 있습니다. 실제 사용자 폴더나 고정된 개인 경로를 대상으로 하지 않습니다.

## 릴리스

공개 릴리스는 GitHub Actions가 깨끗한 체크아웃 상태에서 빌드합니다. 로컬 사용 기록은 포함하지 않습니다.

소스 커밋과 릴리스에서 제외하는 항목:

- `data/`
- 로컬 설정
- 작업 로그
- ACL 백업
- 테스트 실행 폴더
- `bin/`, `obj/`
- 로컬 `release/` 출력물
- `build/.dotnet/` 로컬 SDK 캐시

GitHub Releases에서 최신 ZIP을 다운로드하고 압축을 푼 뒤 다음 파일을 실행합니다.

```text
이은성폴더잠금기.exe
```

Windows에 .NET 8 Desktop Runtime이 없으면 실행 전에 설치가 필요할 수 있습니다.

## 아이콘 생성

앱 아이콘은 프로젝트 루트의 PNG 원본을 기반으로 생성하며, 결과물은 `assets/icons` 아래에 저장합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Generate-EsleeFolderLockIcon.ps1
```

16px부터 256px까지의 PNG 파일과 Windows용 멀티사이즈 `.ico` 파일이 생성됩니다.
