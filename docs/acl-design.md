# eslee폴더잠금기 ACL Design

eslee폴더잠금기(FolderGate 엔진)는 `System.Security.AccessControl` API를 사용하며 잠금 메커니즘으로 항목마다 `icacls /deny`를 실행하지 않습니다.

강화 모드는 승격된 도우미 프로세스 하나에서 실행됩니다. 파일마다 `icacls`, PowerShell, `cmd`를 새로 실행하지 않고 `Directory.EnumerateDirectories`와 `Directory.EnumerateFiles`로 순회합니다.

## Rule Policy

- 앱이 추가한 deny rule만 식별 가능하게 추가합니다.
- 잠금 해제 시 앱이 추가한 SID와 access mask에 일치하는 rule만 제거합니다.
- 잠금 전 ACL은 JSON 백업으로 저장합니다.
- 복구는 백업 파일의 SDDL을 기준으로 원래 ACL을 되돌립니다.

## Backup And Progress

ACL 백업과 작업 로그는 항목마다 즉시 개별 파일로 쓰지 않습니다. 작업 중에는 메모리와 배치 저장을 사용하고, 작업 완료 또는 복구가 필요한 시점에 안전하게 저장합니다.

UI와 진행률 파일에는 총 처리 수, 완료 수, 실패 수, 현재 경로, 경과 시간, 예상 남은 시간이 기록됩니다.

## Rollback

재귀 작업 중 취소나 예외가 발생하면 변경된 경로를 순서대로 추적한 목록을 이용해 역순으로 원복합니다. 원복 실패는 JSON Lines 로그에 남기고 대상 상태를 `RecoveryRequired`로 표시합니다.

## Limits

ACL 규칙은 Windows 메타데이터이며 암호화가 아닙니다. 관리자, 소유자, 백업 운영자, 다른 부팅 환경, ACL을 편집할 수 있는 사용자는 변경 사항을 우회하거나 되돌릴 수 있습니다.
