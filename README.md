# eslee폴더잠금기

[⬇️ **최신 버전 다운로드**](https://github.com/esleeeeee/eslee-folder-locker/releases/latest)

**문서 언어:** 한국어 · [English](README.en.md)

Windows에서 폴더를 잠깐 숨기거나 막아두고 싶은데, 매번 복잡한 권한 설정을 직접 만지기는 부담스러울 때가 있습니다. `eslee폴더잠금기`는 이런 상황을 위해 만든 개인용 Windows 폴더 잠금 프로그램입니다.

이 프로그램은 파일을 암호화하지 않습니다. 대신 Windows NTFS 권한을 조정해서 일반 사용자 상태에서 특정 폴더를 열거나 수정하기 어렵게 만듭니다. 파일은 원래 위치에 그대로 남고, 프로그램이 파일 내용을 읽거나 압축하거나 다른 곳으로 옮기지 않습니다.

내부 프로젝트명과 엔진명은 호환성을 위해 `FolderGate`를 유지합니다. 사용자에게 보이는 이름은 `eslee폴더잠금기`입니다.

이 프로젝트는 전부 바이브코딩으로 구현했습니다. 기능 기획, UI 동작, NTFS ACL 처리, 복구 도구, 테스트, 릴리스 자동화, 문서화까지 자연어로 요구사항을 정리하고 AI 코딩 에이전트와 반복 개발했습니다.

## 어떤 상황에 쓰나요?

가족이나 지인이 내 컴퓨터를 잠깐 사용할 때, 특정 폴더를 파일 탐색기에서 바로 열지 못하게 하고 싶은 경우를 생각하고 만들었습니다.

예를 들면 다음과 같은 상황입니다.

- 작업 중인 개인 폴더를 잠깐 막아두고 싶을 때
- 파일 탐색기에서 실수로 열리거나 수정되는 것을 줄이고 싶을 때
- 암호화 프로그램까지는 필요 없지만 간단한 로컬 접근 제한이 필요할 때
- 잠근 폴더를 나중에 비밀번호로 다시 풀고 싶을 때

단, 이 프로그램은 강력한 보안 제품이 아닙니다. 관리자 권한이 있거나 Windows 권한 설정을 직접 다룰 줄 아는 사용자는 우회할 수 있습니다. 정말 중요한 자료는 Windows 계정 분리, BitLocker 같은 디스크 암호화, 파일 단위 암호화, 전용 보안 솔루션을 사용해야 합니다.

## 기본 사용 흐름

1. 릴리스 페이지에서 한국어 zip 파일을 다운로드합니다.
2. 원하는 위치에 압축을 풉니다.
3. `eslee폴더잠금기.exe`를 실행합니다.
4. 잠글 폴더를 추가합니다.
5. 처음 사용하는 경우 비밀번호를 설정합니다.
6. 빠른 모드 또는 강화 모드로 잠금을 적용합니다.
7. 잠금 해제할 때는 앱에서 비밀번호를 입력하거나, 탐색기 우클릭 메뉴를 사용합니다.

Windows에 .NET 8 Desktop Runtime이 설치되어 있지 않으면 실행 전에 설치가 필요할 수 있습니다.

## 다운로드 파일

릴리스는 한국어 버전과 영어 버전으로 나뉩니다.

- 한국어: `eslee-folder-locker-vX.Y.Z-ko-win-x64.zip`
- 영어: `eslee-folder-locker-vX.Y.Z-en-win-x64.zip`

한국어 zip 안의 주요 실행 파일은 다음과 같습니다.

```text
eslee폴더잠금기.exe
eslee폴더잠금기_권한도우미.exe
eslee폴더잠금기_복구도구.exe
```

일반 사용자는 보통 `eslee폴더잠금기.exe`만 실행하면 됩니다. 권한 도우미와 복구 도구는 잠금, 잠금 해제, 복구 과정에서 필요할 때 사용됩니다.

## 폴더 잠금은 어떻게 동작하나요?

`eslee폴더잠금기`는 Windows NTFS ACL을 사용합니다. ACL은 Windows가 파일과 폴더의 접근 권한을 관리하는 방식입니다.

프로그램은 대략 다음 순서로 동작합니다.

```text
잠금 전 권한 백업
        ↓
현재 Windows 사용자 SID 확인
        ↓
대상 폴더에 접근 거부 권한 추가
        ↓
잠금 해제 시 앱이 추가한 권한만 제거하거나 백업 권한 복구
```

잠금 상태에서는 일반 사용자 권한에서 다음 작업이 실패하도록 설계되어 있습니다.

- 폴더 열기
- 폴더 목록 조회
- 파일 읽기
- 파일 쓰기
- 새 파일 만들기
- 새 하위 폴더 만들기
- 파일 삭제
- 파일 이름 변경
- 외부 파일을 잠긴 폴더로 복사

잠금 전 권한은 JSON 백업 파일로 저장됩니다. 복구 도구는 이 백업을 사용해 원래 ACL로 되돌립니다.

## 빠른 모드와 강화 모드

| 모드 | 설명 | 권장 상황 |
| --- | --- | --- |
| 빠른 모드 | 선택한 폴더 루트에만 잠금을 적용합니다. | 빠르게 파일 탐색기 진입을 막고 싶을 때 |
| 강화 모드 | 하위 폴더와 파일을 재귀적으로 순회하며 각 항목의 ACL을 백업하고 변경합니다. | 폴더 내부 항목까지 더 강하게 막고 싶을 때 |

강화 모드는 항목 수가 많으면 시간이 걸릴 수 있습니다. 대신 파일마다 `icacls`, PowerShell, `cmd.exe`를 새로 실행하지 않습니다. 승격된 helper 프로세스 하나가 .NET 파일 순회 API와 Windows ACL API로 직접 처리합니다.

## 탐색기 우클릭으로 잠금 해제하기

앱에서 탐색기 메뉴를 등록하면 잠긴 폴더를 우클릭해서 바로 잠금 해제 창을 열 수 있습니다.

Windows 11에서는 메뉴가 `더 많은 옵션 표시` 아래에 보일 수 있습니다. `Shift + 우클릭`을 사용하면 확장 우클릭 메뉴를 바로 열 수 있습니다.

한국어 메뉴 이름은 다음과 같습니다.

```text
eslee폴더잠금기로 잠금 해제
```

비밀번호가 맞으면 잠금 해제 시간을 선택할 수 있습니다.

- 1분
- 5분
- 10분
- 30분
- 1시간
- 하루
- 완전 해제

시간 제한 해제를 선택하면 정해진 시간이 지난 뒤 자동으로 다시 잠금을 시도합니다. PC를 꺼둔 동안 만료 시간이 지났다면 다음 Windows 로그인 후 바로 다시 잠금을 시도합니다.

## 프로그램 폴더를 삭제하면 어떻게 되나요?

잠긴 폴더가 남아 있는 상태에서 프로그램 폴더를 삭제하지 마세요.

잠금 정보는 실행 파일 안에만 있는 것이 아닙니다. Windows 파일 시스템의 ACL에 권한 변경이 남아 있고, 실행 파일 옆의 `data` 폴더에는 잠금 해제와 복구에 필요한 설정, 상태, ACL 백업 파일이 들어 있습니다.

프로그램을 삭제했는데 잠긴 폴더가 남아 있다면 다음 순서로 처리하세요.

1. 먼저 휴지통에서 삭제한 프로그램 폴더를 복원합니다.
2. 복원이 어렵다면 GitHub에서 같은 버전 또는 더 최신 버전을 다시 다운로드합니다.
3. 보관 중인 `data` 폴더가 있다면 실행 파일 옆에 다시 둡니다.
4. `eslee폴더잠금기_복구도구.exe`를 관리자 권한으로 실행합니다.
5. 사용할 ACL 백업을 선택해 복구합니다.

`data` 폴더와 ACL 백업까지 모두 삭제했다면 프로그램이 원래 권한을 자동으로 재구성할 수 없습니다. 이 경우 Windows 관리자 권한으로 폴더 속성의 보안 탭을 직접 확인하거나, `icacls` 같은 도구로 Deny 권한을 수동으로 제거해야 합니다.

## 잠금 대상으로 막는 경로

복구가 어려워지는 상황을 줄이기 위해 위험한 경로는 잠금 대상으로 등록하지 못하게 했습니다.

- 드라이브 루트
- Windows 시스템 폴더
- Program Files
- ProgramData
- 사용자 프로필 루트
- OneDrive 루트
- 이 프로젝트 폴더와 그 상위 경로

예를 들어 `C:\`, `D:\`, `C:\Windows`, 사용자 폴더 전체 같은 경로는 잠그지 않는 것이 안전합니다.

## 테스트한 내용

통합 테스트는 실제 사용자 폴더가 아니라 `tests` 아래 임시 폴더에서만 수행하도록 설계했습니다.

검증한 주요 내용은 다음과 같습니다.

- 잠금 상태에서 폴더 열기, 목록 조회, 파일 읽기/쓰기, 생성, 삭제, 이름 변경, 복사가 차단되는지 확인
- 잠금 해제 후 원래 ACL SDDL과 동일하게 복구되는지 확인
- 취소나 오류 발생 시 이미 변경한 항목을 역순으로 원복하는지 확인
- RecoveryTool을 별도 프로세스로 실행해 ACL 백업 복구가 가능한지 확인
- 10,000개 이상 항목에서 강화 모드가 파일마다 외부 프로세스를 실행하지 않는지 확인
- UTC로 저장한 백업 시간이 사용자 화면에서는 로컬 시간으로 표시되는지 확인
- 비밀번호 검증과 WPF 대화상자 레이아웃 확인

## 개발자용 실행

직접 빌드하려면 .NET 8 SDK가 필요합니다.

```powershell
git clone https://github.com/esleeeeee/eslee-folder-locker.git
Set-Location eslee-folder-locker
dotnet restore .\FolderGate.sln
dotnet build .\FolderGate.sln
```

한국어 UI 빌드:

```powershell
dotnet build .\FolderGate.sln -p:AppLanguage=ko
```

영어 UI 빌드:

```powershell
dotnet build .\FolderGate.sln -p:AppLanguage=en
```

일반 테스트:

```powershell
dotnet test .\FolderGate.sln --filter "TestCategory!=RequiresElevation"
```

관리자 권한이 필요한 복구 테스트:

```powershell
dotnet test .\tests\FolderGate.IntegrationTests\FolderGate.IntegrationTests.csproj --filter "TestCategory=RequiresElevation"
```

관리자 권한 테스트는 반드시 관리자 권한 터미널에서 실행해야 합니다.

## 코드 구조

```text
src/
  FolderGate.App/             WPF 메인 앱
  FolderGate.Core/            모델, 경로 검증, 비밀번호, ACL, 저장소 로직
  FolderGate.ElevatedHelper/  UAC 승격 후 실제 ACL 작업 수행
  FolderGate.RecoveryTool/    독립 ACL 복구 도구

tests/
  FolderGate.App.Tests/
  FolderGate.Core.Tests/
  FolderGate.IntegrationTests/

assets/icons/
  앱 아이콘 원본과 Windows ICO

tools/
  아이콘 생성 스크립트
```

## 사용 기술

- C#
- .NET 8
- WPF
- Windows NTFS ACL
- `System.Security.AccessControl`
- PBKDF2-SHA256
- JSON / JSON Lines
- MSTest
- GitHub Actions

## 현재 범위와 주의사항

이 프로그램은 개인 Windows PC에서 가벼운 로컬 접근 제한을 제공하는 도구입니다.

다음 상황은 막을 수 없습니다.

- 관리자 권한 사용자
- 소유권을 가져가거나 ACL을 직접 수정할 수 있는 사용자
- 오프라인 디스크 접근
- 악성코드
- 포렌식 도구
- 백업 운영자 권한

중요한 자료는 이 프로그램 하나에만 의존하지 마세요. 강한 보안이 필요하면 BitLocker, Windows 계정 분리, 전용 암호화 도구를 함께 사용해야 합니다.

## License

MIT License
