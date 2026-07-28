---
name: balance-scriptableobject-sync
description: "밸런스 수치(ScriptableObject 필드)를 추가·수정할 때 사용한다. docs/balance-and-telemetry.md의 표와 SO 필드 이름·값이 일치하는지 확인한다."
---

## Purpose
밸런스 수치가 문서와 ScriptableObject 자산 사이에서 어긋나지 않게 유지한다.

## Use When
- 새 밸런스 값을 ScriptableObject 필드로 추가할 때
- 기존 밸런스 값을 플레이 테스트 결과로 조정할 때
- `docs/balance-and-telemetry.md`를 수정하거나 수정해야 할 때

## Inputs
- `docs/balance-and-telemetry.md`
- 대상 ScriptableObject 자산 (`SO_GameBalance_*`)

## Process
1. 문서의 밸런스 키 이름과 SO 필드 이름이 project-structure.md §12 예시처럼 매핑되는지 확인한다.
   ```text
   GDD: 스피커 쿨타임 45초
   Balance key: speakerCooldownSeconds
   SO field: SpeakerCooldownSeconds
   ```
2. 값을 바꾼 경우, 문서 표와 SO 값 양쪽을 함께 수정한다. 한쪽만 고치고 끝내지 않는다.
3. `Default` / `Playtest` / `Demo` 프로필 중 어느 것을 수정하는지 확인한다.
4. 이 변경이 "밸런스 수치"인지 "핵심 규칙"인지 다시 한번 판단한다 — 핵심 규칙이면
   이 스킬 범위를 벗어나므로 AGENTS.md의 "바이브코딩 중 기획 변경 대응" 절차를 따른다.

## Outputs
- 문서-SO 매핑 표 (변경 전/후)
- 갱신된 `docs/devlog.md` 항목 초안 (한 줄)

## Quality Bar
- 문서와 SO 값이 하나라도 어긋난 채 남아있으면 완료로 보지 않는다.

## Common Failure Modes
- SO 값만 바꾸고 문서를 갱신하지 않음
- 잘못된 프로필(Demo용 값을 Default에)을 수정함

## Related Agents
- `game-developer`, `qa-expert` (밸런스 테스트)
