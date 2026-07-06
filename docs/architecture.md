# 이은성폴더잠금기 Architecture

이은성폴더잠금기(FolderGate 엔진)는 NTFS ACL 메타데이터를 변경해 파일 탐색기의 일반 접근을 차단하는 로컬 전용 Windows 11 WPF 애플리케이션입니다. 파일 내용을 암호화, 복사, 이동, 압축, 이름 변경, 재작성, 검사하지 않습니다.

## Projects

- `FolderGate.App`: 한국어 WPF UI입니다. 일반 사용자 권한으로 실행되며 잠금, 잠금 해제, 복구 작업이 필요할 때만 UAC 승격을 요청합니다.
- `FolderGate.Core`: 도메인 모델, 비밀번호 해시, JSON 설정/로그 저장소, 경로 검증, ACL 백업 직렬화, ACL 변경 서비스를 포함합니다.
- `FolderGate.ElevatedHelper`: 승격 실행되는 콘솔 도우미입니다. 배포 alias는 `이은성폴더잠금기_권한도우미.exe`입니다.
- `FolderGate.RecoveryTool`: 독립 콘솔 복구 도구입니다. 배포 alias는 `이은성폴더잠금기_복구도구.exe`입니다.
- `FolderGate.Core.Tests`, `FolderGate.App.Tests`: 단위 및 UI 검증 테스트입니다.
- `FolderGate.IntegrationTests`: `tests` 아래 임시 폴더만 사용하는 ACL 통합 테스트입니다.

## Flow

1. 사용자가 메인 UI에서 폴더를 등록합니다.
2. `TargetPathValidator`가 드라이브 루트, Windows 시스템 폴더, 사용자 프로필 루트, OneDrive 루트, 프로젝트 루트 등 위험 경로를 차단합니다.
3. 최초 등록 시 비밀번호를 설정합니다. 비밀번호는 최소 4자 이상이며 PBKDF2-SHA256 hash와 salt만 저장됩니다.
4. 잠금 또는 잠금 해제 요청 시 UI가 비밀번호와 사용자 확인을 처리합니다.
5. 실제 ACL 작업은 `FolderGate.ElevatedHelper.exe` 또는 `이은성폴더잠금기_권한도우미.exe`를 `runas`로 실행해 수행합니다.
6. 복구 도구는 ACL 백업 파일을 선택받고 `RESTORE` 확인 후 원래 ACL을 복구합니다.

## Security Boundary

이은성폴더잠금기는 암호화 보안 경계가 아닙니다. 관리자, 소유자, 백업 운영자, ACL 지식이 있는 사용자는 잠금을 우회하거나 복구할 수 있습니다. 앱의 목적은 파일 탐색기를 통한 우발적이거나 가벼운 접근을 줄이는 것입니다.
