# Changelog

## v1.1.0

### English

- Renamed the public product to `eslee폴더잠금기` and standardized the English name as `eslee folder locker`.
- Renamed repository/release naming to `eslee-folder-locker`.
- Replaced the previous icon set with the new `eslee-folder-lock.png` based icon assets.
- Added separate Korean and English release packages.
- Added build-time localization through `AppLanguage=ko|en` for UI, dialogs, helper console text, recovery tool text, progress messages, and validation messages.
- Updated File Explorer context-menu command naming and release package aliases.

### Korean

- 사용자 표시 제품명을 `eslee폴더잠금기`로 변경하고 영문 이름을 `eslee folder locker`로 통일했습니다.
- 저장소/릴리즈 표기를 `eslee-folder-locker`로 변경했습니다.
- 기존 아이콘 세트를 제거하고 새 `eslee-folder-lock.png` 기반 아이콘 자산을 적용했습니다.
- 한국어/영어 릴리즈 패키지를 분리했습니다.
- `AppLanguage=ko|en` 빌드 속성으로 UI, 대화상자, 권한 도우미 콘솔, 복구 도구, 진행 메시지, 검증 메시지를 언어별로 제공합니다.
- File Explorer 우클릭 메뉴 명칭과 릴리즈 실행 파일 별칭을 갱신했습니다.

## v1.0.3

### English

- Added Explorer context-menu unlock flow for registered locked folders.
- Added a password unlock dialog with duration choices: 1 minute, 5 minutes, 10 minutes, 30 minutes, 1 hour, 1 day, or permanent unlock.
- Added temporary unlock state tracking with automatic re-lock after the selected absolute expiration time.
- Added login-time temporary unlock recovery. If Windows is shut down before the selected time expires, the app resumes the remaining timer at next login; if the expiration time already passed, it attempts to re-lock immediately.
- Improved unlock popup placement. Ownerless dialogs launched from Explorer are now centered on the monitor where the mouse is located, with DPI-aware positioning.
- Hid manual auto-relock controls from the main UI. Auto-relock registration is handled internally when temporary unlock is used.
- Added tests for startup arguments, Explorer context-menu command generation, temporary unlock state display, unlock duration UI, and login-time relock selection.

### Korean

- 등록된 잠금 폴더를 탐색기 우클릭 메뉴에서 잠금 해제할 수 있는 흐름을 추가했습니다.
- 비밀번호 입력 후 1분, 5분, 10분, 30분, 1시간, 하루, 완전 해제를 선택할 수 있는 잠금 해제 대화상자를 추가했습니다.
- 선택한 절대 만료 시각에 맞춰 다시 잠그는 임시 해제 상태 추적을 추가했습니다.
- Windows 종료/재부팅 후 다음 로그인 시 임시 해제 상태를 복구합니다. 만료 시각이 지나 있으면 즉시 다시 잠금을 시도하고, 아직 남아 있으면 남은 시간만큼 대기한 뒤 다시 잠급니다.
- 탐색기에서 실행된 비밀번호 팝업 위치를 개선했습니다. Owner가 없는 대화상자는 현재 마우스가 있는 모니터의 중앙에 DPI 보정 후 표시됩니다.
- 수동 자동 재잠금 등록/제거 버튼을 UI에서 제거했습니다. 임시 해제를 사용하면 내부에서 자동 등록됩니다.
- 시작 인자, 탐색기 메뉴 명령 생성, 임시 해제 상태 표시, 해제 시간 선택 UI, 로그인 시 재잠금 대상 선택 테스트를 추가했습니다.
