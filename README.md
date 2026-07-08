# eslee folder locker

eslee folder locker is a Windows desktop utility that limits casual access to selected local folders by changing NTFS permissions. It is not a file encryption product. The files stay in place, and the program does not rewrite, move, compress, or inspect file contents.

The public Korean product name is `eslee폴더잠금기`. The internal engine, solution, project names, and namespaces still use `FolderGate` for compatibility.

This project was implemented entirely through vibe coding. Product direction, UI behavior, NTFS ACL handling, recovery tooling, tests, release automation, and documentation were iterated through natural-language collaboration with an AI coding agent.

## What It Does

The app lets a user register local NTFS folders and apply an access-deny lock to them. While locked, normal user-context operations should fail in File Explorer and ordinary file APIs, including opening the folder, listing contents, reading files, writing files, creating files or folders, renaming, deleting, and copying external files into the locked target.

The target use case is lightweight local access control on a personal Windows PC. It is meant to reduce accidental or casual browsing, not to defend against administrators, forensic tools, malware, offline disk access, or users who understand NTFS ACLs.

## How Folder Locking Works

eslee folder locker uses Windows NTFS ACLs rather than encryption.

- Before locking, the original ACL state is backed up as JSON.
- A deny rule is added for the current Windows user SID.
- Unlock removes the deny rule added by the app or restores the backed-up ACL.
- Recovery uses the saved ACL backup to restore the original permissions.
- User-facing times are shown in local time; technical timestamps stored in JSON remain UTC.

Lock modes:

- Quick mode: applies the lock to the selected folder root only. This is fast and intended to block common File Explorer entry into the folder.
- Hardened mode: recursively enumerates child folders and files, backs up each item ACL, and applies ACL changes item by item.

Hardened mode runs inside a single elevated helper process. It does not start `icacls`, PowerShell, or `cmd.exe` once per file. The implementation uses .NET directory enumeration and Windows ACL APIs directly.

## Recovery And Deletion Warning

Remember where you extracted or installed the program. The `data` directory beside the executables contains configuration, operation state, and ACL backup files needed for unlock and recovery.

Do not delete the program folder while folders are still locked. Deleting the program does not automatically undo NTFS ACL rules because the permissions live on the file system, not inside the executable.

Recovery tool:

- Korean build: `eslee폴더잠금기_복구도구.exe`
- English build: `eslee-folder-locker-recovery.exe`
- Internal project executable: `FolderGate.RecoveryTool.exe`

If the program was deleted while folders were locked, first restore the deleted program folder from the Recycle Bin. If that is not possible, download the same or a newer release, extract it, and place any preserved `data` directory back beside the executables before running the recovery tool. If the ACL backup data was also deleted, the app cannot reconstruct the original ACL automatically; a Windows administrator must manually inspect the folder permissions and remove deny rules or repair the ACL.

## Explorer Unlock Shortcut

The app can register a File Explorer context-menu unlock command. After registering it in the app, right-click a registered locked folder and choose the unlock command.

On Windows 11, the entry may appear under `Show more options`. `Shift` + right-click opens the expanded context menu directly.

Menu text:

- Korean build: `eslee폴더잠금기로 잠금 해제`
- English build: `Unlock with eslee Folder Locker`

After entering the password, the unlock dialog lets you choose a duration: 1 minute, 5 minutes, 10 minutes, 30 minutes, 1 hour, 1 day, or permanent unlock. Temporary unlock uses an absolute UTC expiration time, so if the PC is shut down before the selected time expires, the app attempts to relock on the next Windows login. If the expiration time already passed while the PC was off, it attempts to relock immediately after startup.

## Security Notes

This is not a cryptographic security boundary.

Users with administrator rights, ownership rights, ACL knowledge, offline disk access, backup operator privileges, malware, or forensic tooling can bypass or reverse the lock. For high-security use cases, use full-disk encryption, per-file encryption, account separation, Windows security policy, or dedicated security software.

The app intentionally blocks risky targets such as:

- Drive roots
- Windows system folders
- Program Files and ProgramData
- User profile root
- OneDrive root
- The project folder and its parent paths

These restrictions reduce the chance of locking system paths or making recovery tools inaccessible.

## Technology

- Language: C#
- Runtime: .NET 8
- UI: WPF
- Platform: Windows 11 / NTFS
- ACL handling: `System.Security.AccessControl`
- Password hashing: PBKDF2-SHA256
- Storage: JSON configuration, JSON ACL backups, JSON Lines operation logs
- Tests: MSTest
- Release automation: GitHub Actions

## Project Layout

```text
src/
  FolderGate.App/             WPF desktop app
  FolderGate.Core/            Models, validation, password hashing, ACL logic, storage
  FolderGate.ElevatedHelper/  Elevated lock/unlock/restore helper
  FolderGate.RecoveryTool/    Standalone ACL recovery console tool

tests/
  FolderGate.App.Tests/
  FolderGate.Core.Tests/
  FolderGate.IntegrationTests/

assets/icons/
  Source PNG, generated PNG sizes, and Windows ICO

tools/
  Icon generation script
```

## Build

```powershell
dotnet restore .\FolderGate.sln
dotnet build .\FolderGate.sln
```

Build the English UI:

```powershell
dotnet build .\FolderGate.sln -p:AppLanguage=en
```

Build the Korean UI explicitly:

```powershell
dotnet build .\FolderGate.sln -p:AppLanguage=ko
```

## Test

```powershell
dotnet test .\FolderGate.sln --filter "TestCategory!=RequiresElevation"
```

Elevation-required ACL recovery tests should be run separately from an elevated terminal:

```powershell
dotnet test .\tests\FolderGate.IntegrationTests\FolderGate.IntegrationTests.csproj --filter "TestCategory=RequiresElevation"
```

Integration tests are designed to use temporary folders under `tests`; they must not target real user folders or fixed personal paths.

## Release Packages

Public releases are built by GitHub Actions from a clean checkout. Runtime state is excluded from source commits and release assets:

- Local settings
- Operation logs
- ACL backups
- Test run folders
- Local `release/` output
- Local SDK cache under `build/.dotnet/`

Release assets are split by language:

- `eslee-folder-locker-vX.Y.Z-ko-win-x64.zip`
- `eslee-folder-locker-vX.Y.Z-en-win-x64.zip`

Run files:

```text
Korean:
  eslee폴더잠금기.exe
  eslee폴더잠금기_권한도우미.exe
  eslee폴더잠금기_복구도구.exe

English:
  eslee-folder-locker.exe
  eslee-folder-locker-helper.exe
  eslee-folder-locker-recovery.exe
```

Windows may require the .NET 8 Desktop Runtime if it is not already installed.

## Icon Generation

The official icon source is:

```text
eslee-folder-lock.png
```

Generate Windows icon assets:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Generate-EsleeFolderLockerIcon.ps1
```

Generated assets:

```text
assets/icons/eslee-folder-locker-source.png
assets/icons/eslee-folder-locker-16.png
assets/icons/eslee-folder-locker-24.png
assets/icons/eslee-folder-locker-32.png
assets/icons/eslee-folder-locker-48.png
assets/icons/eslee-folder-locker-64.png
assets/icons/eslee-folder-locker-128.png
assets/icons/eslee-folder-locker-256.png
assets/icons/eslee-folder-locker.ico
```

---

# eslee폴더잠금기

eslee폴더잠금기는 Windows에서 선택한 로컬 폴더의 일반적인 접근을 NTFS 권한 변경으로 제한하는 데스크톱 유틸리티입니다. 파일 암호화 제품이 아닙니다. 파일은 원래 위치에 그대로 남고, 프로그램은 파일 내용을 다시 쓰거나 이동하거나 압축하거나 검사하지 않습니다.

내부 엔진, 솔루션명, 프로젝트명, 네임스페이스는 호환성을 위해 `FolderGate`를 유지합니다.

이 프로젝트는 전부 바이브코딩으로 구현되었습니다. 제품 방향, UI 동작, NTFS ACL 처리, 복구 도구, 테스트, 릴리스 자동화, 문서화까지 AI 코딩 에이전트와 자연어로 협업하며 반복 구현했습니다.

## 하는 일

사용자는 로컬 NTFS 폴더를 등록하고 접근 거부 잠금을 적용할 수 있습니다. 잠금 상태에서는 일반 사용자 컨텍스트에서 폴더 열기, 목록 조회, 파일 읽기/쓰기, 새 파일/폴더 생성, 이름 변경, 삭제, 외부 파일 복사 같은 작업이 실패하도록 설계되어 있습니다.

목표는 개인 Windows PC에서의 가벼운 로컬 접근 제한입니다. 관리자, 포렌식 도구, 악성코드, 오프라인 디스크 접근, NTFS ACL을 이해하는 사용자를 막기 위한 보안 경계가 아닙니다.

## 잠금 구현 방식

eslee폴더잠금기는 암호화 대신 Windows NTFS ACL을 사용합니다.

- 잠금 전 원래 ACL 상태를 JSON으로 백업합니다.
- 현재 Windows 사용자 SID에 deny rule을 추가합니다.
- 잠금 해제 시 앱이 추가한 deny rule을 제거하거나 백업된 ACL을 복구합니다.
- 복구 도구는 저장된 ACL 백업으로 원래 권한을 복원합니다.
- 사용자에게 보이는 시간은 로컬 시간으로 표시하고, JSON 내부 기술 timestamp는 UTC를 유지합니다.

잠금 모드:

- 빠른 모드: 선택한 폴더 루트에만 잠금을 적용합니다. 파일 탐색기에서 폴더에 들어가는 일반적인 접근을 빠르게 막는 용도입니다.
- 강화 모드: 하위 폴더와 파일을 재귀적으로 순회하면서 각 항목의 ACL을 백업하고 변경합니다.

강화 모드는 UAC로 승격된 도우미 프로세스 하나에서 실행됩니다. 파일마다 `icacls`, PowerShell, `cmd.exe`를 새로 실행하지 않고 .NET 파일 시스템 순회 API와 Windows ACL API를 직접 사용합니다.

## 복구와 삭제 주의사항

프로그램을 어디에 압축 해제했거나 설치했는지 반드시 기억하세요. 실행 파일 옆의 `data` 디렉터리에는 잠금 해제와 복구에 필요한 설정, 작업 상태, ACL 백업 파일이 저장됩니다.

폴더가 잠긴 상태에서 프로그램 폴더를 삭제하지 마세요. 프로그램을 삭제해도 NTFS ACL 규칙은 자동으로 사라지지 않습니다. 권한은 실행 파일 안이 아니라 파일 시스템에 남아 있습니다.

복구 도구:

- 한국어 빌드: `eslee폴더잠금기_복구도구.exe`
- 영어 빌드: `eslee-folder-locker-recovery.exe`
- 내부 프로젝트 실행 파일: `FolderGate.RecoveryTool.exe`

잠긴 폴더가 남아 있는 상태에서 프로그램을 삭제했다면 먼저 휴지통에서 프로그램 폴더를 복원하세요. 복원이 어렵다면 같은 버전 또는 더 최신 릴리스를 다시 다운로드해 압축을 풀고, 보관 중인 `data` 디렉터리가 있다면 실행 파일 옆에 다시 배치한 뒤 복구 도구를 실행하세요. ACL 백업 데이터까지 삭제된 경우 앱은 원래 ACL을 자동으로 재구성할 수 없습니다. 이 경우 Windows 관리자 권한으로 폴더 권한을 직접 확인해 deny 규칙을 제거하거나 ACL을 수동 복구해야 합니다.

## 탐색기 잠금 해제 바로가기

앱에서 File Explorer 우클릭 메뉴를 등록할 수 있습니다. 등록 후 잠긴 폴더를 우클릭해 잠금 해제 명령을 실행할 수 있습니다.

Windows 11에서는 이 항목이 `더 많은 옵션 표시` 아래에 보일 수 있습니다. `Shift` + 우클릭을 사용하면 확장 우클릭 메뉴를 바로 열 수 있습니다.

메뉴 문구:

- 한국어 빌드: `eslee폴더잠금기로 잠금 해제`
- 영어 빌드: `Unlock with eslee Folder Locker`

비밀번호 입력 후 1분, 5분, 10분, 30분, 1시간, 하루, 완전 해제를 선택할 수 있습니다. 시간 제한 해제는 절대 UTC 만료 시각을 저장합니다. 선택한 시간이 끝나기 전에 PC를 껐다면 다음 Windows 로그인 시 다시 잠금을 시도합니다. PC가 꺼져 있는 동안 이미 만료 시각이 지났다면 부팅 후 즉시 다시 잠금을 시도합니다.

## 보안상 주의사항

이 프로그램은 암호화 보안 경계가 아닙니다.

관리자 권한, 소유자 권한, ACL 지식, 오프라인 디스크 접근, 백업 운영자 권한, 악성코드, 포렌식 도구를 막을 수 없습니다. 높은 보안이 필요한 경우 전체 디스크 암호화, 파일 단위 암호화, Windows 계정 분리, 보안 정책, 전용 보안 솔루션을 사용해야 합니다.

기본적으로 다음 경로는 잠금 대상으로 막습니다.

- 드라이브 루트
- Windows 시스템 폴더
- Program Files 및 ProgramData
- 사용자 프로필 루트
- OneDrive 루트
- 프로젝트 폴더와 그 상위 경로

이 제한은 시스템 경로나 복구 도구 접근 경로를 잠가 복구가 어려워지는 상황을 줄이기 위한 안전장치입니다.

## 사용 기술

- 언어: C#
- 런타임: .NET 8
- UI: WPF
- 대상 플랫폼: Windows 11 / NTFS
- ACL 처리: `System.Security.AccessControl`
- 비밀번호 해싱: PBKDF2-SHA256
- 저장 방식: JSON 설정, JSON ACL 백업, JSON Lines 작업 로그
- 테스트: MSTest
- 릴리스 자동화: GitHub Actions

## 빌드

```powershell
dotnet restore .\FolderGate.sln
dotnet build .\FolderGate.sln
```

영어 UI 빌드:

```powershell
dotnet build .\FolderGate.sln -p:AppLanguage=en
```

한국어 UI 명시 빌드:

```powershell
dotnet build .\FolderGate.sln -p:AppLanguage=ko
```

## 테스트

```powershell
dotnet test .\FolderGate.sln --filter "TestCategory!=RequiresElevation"
```

관리자 권한이 필요한 ACL 복구 테스트는 관리자 권한 터미널에서 별도로 실행합니다.

```powershell
dotnet test .\tests\FolderGate.IntegrationTests\FolderGate.IntegrationTests.csproj --filter "TestCategory=RequiresElevation"
```

통합 테스트는 `tests` 아래 임시 폴더만 사용하도록 설계되어 있습니다. 실제 사용자 폴더나 고정된 개인 경로를 대상으로 하지 않습니다.

## 릴리스 패키지

공개 릴리스는 GitHub Actions가 깨끗한 체크아웃 상태에서 빌드합니다. 다음 로컬 상태는 소스 커밋과 릴리스 산출물에 포함하지 않습니다.

- 로컬 설정
- 작업 로그
- ACL 백업
- 테스트 실행 폴더
- 로컬 `release/` 출력물
- `build/.dotnet/` 로컬 SDK 캐시

릴리스 파일은 언어별로 나뉩니다.

- `eslee-folder-locker-vX.Y.Z-ko-win-x64.zip`
- `eslee-folder-locker-vX.Y.Z-en-win-x64.zip`

실행 파일:

```text
한국어:
  eslee폴더잠금기.exe
  eslee폴더잠금기_권한도우미.exe
  eslee폴더잠금기_복구도구.exe

영어:
  eslee-folder-locker.exe
  eslee-folder-locker-helper.exe
  eslee-folder-locker-recovery.exe
```

Windows에 .NET 8 Desktop Runtime이 없으면 실행 전에 설치가 필요할 수 있습니다.

## 아이콘 생성

공식 아이콘 원본:

```text
eslee-folder-lock.png
```

아이콘 생성:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Generate-EsleeFolderLockerIcon.ps1
```

결과물은 `assets/icons` 아래에 16px부터 256px까지의 PNG와 Windows 멀티사이즈 ICO로 저장됩니다.
