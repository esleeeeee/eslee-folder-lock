# 이은성폴더잠금기

이은성폴더잠금기(FolderGate 엔진)는 Windows 11 개인용 NTFS 폴더 잠금 도구입니다. 파일 내용을 암호화하는 보안 제품이 아니라, 컴퓨터에 익숙하지 않은 사용자가 파일 탐색기로 특정 폴더를 여는 것을 막기 위한 가벼운 로컬 프로그램입니다.

## 목적과 보안 한계

- 이은성폴더잠금기는 NTFS ACL 권한 메타데이터를 변경해 일반적인 파일 탐색기 접근을 차단합니다.
- 파일 내용, 파일명, 폴더 구조를 암호화하거나 복사하거나 이동하지 않습니다.
- 관리자 권한이 있거나 NTFS ACL을 이해하는 사용자는 잠금을 우회하거나 수동으로 해제할 수 있습니다.
- 커널 드라이버, 파일 시스템 필터 드라이버, 레지스트리 해킹, 가상 드라이브, 클라우드 API를 사용하지 않습니다.

## 대용량 파일 처리

이은성폴더잠금기는 파일 내용을 읽거나 복사하지 않습니다. 빠른 모드는 선택한 폴더 루트의 ACL만 바꾸므로 파일 크기와 거의 무관하게 빠르게 끝납니다. 강화 모드는 하위 파일과 폴더의 ACL을 재귀 처리하므로 파일 크기가 아니라 파일 및 폴더 개수에 따라 시간이 달라질 수 있습니다.

강화 모드에서도 파일마다 `icacls`, PowerShell, `cmd` 같은 외부 프로세스를 새로 실행하지 않습니다. UAC로 승격된 `FolderGate.ElevatedHelper.exe` 또는 배포 alias인 `이은성폴더잠금기_권한도우미.exe` 프로세스 하나가 `Directory.EnumerateDirectories`와 `Directory.EnumerateFiles` 기반으로 순회하면서 ACL 백업과 변경을 직접 수행합니다.

## 빠른 모드와 강화 모드

- 빠른 모드: 선택한 폴더 루트에만 접근 거부 ACL을 추가합니다. 파일 탐색기 더블클릭 접근 차단용이며 빠릅니다.
- 강화 모드: 선택한 폴더와 하위 파일 및 폴더에 직접 ACL을 적용합니다. UI에는 총 처리 수, 완료 수, 실패 수, 현재 경로, 경과 시간, 예상 남은 시간이 표시됩니다.
- 취소 또는 오류가 발생하면 이미 변경된 항목 목록을 이용해 가능한 범위에서 역순으로 원복합니다.

## 차단 대상

다음 대상은 기본적으로 잠금 대상으로 사용할 수 없습니다.

- 드라이브 루트
- Windows, Program Files, Program Files (x86), ProgramData
- 사용자 프로필 루트
- OneDrive 루트
- 이은성폴더잠금기 프로젝트 루트와 그 하위 폴더
- 이은성폴더잠금기 프로젝트 루트의 상위 폴더

## 비밀번호

비밀번호는 최소 4자 이상이어야 합니다. 평문은 저장하지 않고, 설정 파일에는 PBKDF2-SHA256 salt와 hash만 저장합니다. 비밀번호가 틀리면 잠금 해제나 비밀번호 변경을 위한 ACL 작업은 수행하지 않습니다.

## 프로그램 삭제와 복구

이은성폴더잠금기 프로그램 파일을 삭제해도 이미 적용된 Windows 권한은 자동으로 풀리지 않습니다. NTFS ACL은 Windows 파일 시스템에 남아 있으므로 이은성폴더잠금기 복구 도구 또는 Windows 권한 편집으로 명시적으로 복구해야 합니다.

복구 도구 사용 방법:

1. `release\이은성폴더잠금기_복구도구.exe`를 관리자 권한으로 실행합니다.
2. 복구할 잠금 대상을 선택합니다.
3. 사용할 `data\backups` ACL 백업 파일을 선택합니다.
4. 대상 경로와 복구 항목 수를 확인합니다.
5. `RESTORE`를 입력해 복구를 실행합니다.

내부 호환성을 위해 `FolderGate.RecoveryTool.exe`도 빌드 산출물에 남을 수 있습니다.

## 빌드와 테스트

저장소에는 로컬 SDK가 없던 환경을 고려해 `build\.dotnet\dotnet.exe`를 사용할 수 있게 되어 있습니다. 시스템에 .NET 8 SDK가 있으면 `dotnet`으로 바꿔 실행해도 됩니다.

```powershell
.\build\.dotnet\dotnet.exe restore .\FolderGate.sln
.\build\.dotnet\dotnet.exe build .\FolderGate.sln
.\build\.dotnet\dotnet.exe test .\FolderGate.sln
```

권한 승격이 필요한 통합 테스트는 관리자 터미널에서 실행해야 합니다. 일반 터미널에서는 다음처럼 승격 필요 테스트를 제외할 수 있습니다.

```powershell
.\build\.dotnet\dotnet.exe test .\FolderGate.sln --filter "TestCategory!=RequiresElevation"
```

## 실행 및 배포 파일

배포용 실행 파일은 `release` 폴더에 생성합니다.

권장 사용자 실행 파일명:

- `이은성폴더잠금기.exe`
- `이은성폴더잠금기_복구도구.exe`

내부 호환성을 위해 다음 기술 파일명도 함께 남을 수 있습니다.

- `FolderGate.App.exe`
- `FolderGate.ElevatedHelper.exe`
- `FolderGate.RecoveryTool.exe`

메인 UI는 일반 권한으로 실행됩니다. 실제 잠금, 잠금 해제, 복구 작업을 수행할 때만 이은성폴더잠금기 권한 도우미 또는 이은성폴더잠금기 복구 도구가 UAC 승격을 요청합니다.

## GitHub Releases

GitHub에서 배포 파일을 받으려면 저장소의 **Releases** 화면에서 최신 `이은성폴더잠금기-<version>-win-x64.zip` 파일을 다운로드합니다.

릴리스 ZIP은 빌드 서버에서 새로 생성한 초기 상태 산출물만 포함합니다. 로컬 사용 중 생성되는 `data`, 설정, 작업 로그, ACL 백업, 테스트 실행 폴더는 포함하지 않습니다.

압축을 푼 뒤 `이은성폴더잠금기.exe`를 실행합니다. Windows에 .NET 8 Desktop Runtime이 없으면 실행 전에 런타임 설치가 필요할 수 있습니다.

## 앱 아이콘 생성

공식 앱 아이콘 원본은 프로젝트 루트의 `12cc6217-3fb5-4bf2-8821-ea6b53083df4.png`입니다. 원본 파일은 삭제하거나 덮어쓰지 않고, 아래 명령으로 `assets\icons`에 복사본과 파생 아이콘을 생성합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Generate-EunsungFolderLockIcon.ps1
```

생성되는 파일:

- `assets\icons\EunsungFolderLock-source.png`
- `assets\icons\EunsungFolderLock.ico`
- `assets\icons\EunsungFolderLock-16.png`
- `assets\icons\EunsungFolderLock-24.png`
- `assets\icons\EunsungFolderLock-32.png`
- `assets\icons\EunsungFolderLock-48.png`
- `assets\icons\EunsungFolderLock-64.png`
- `assets\icons\EunsungFolderLock-128.png`
- `assets\icons\EunsungFolderLock-256.png`

`EunsungFolderLock.ico`는 Debug 및 Release 빌드의 메인 앱, 복구 도구, 권한 도우미 EXE에 적용됩니다.
