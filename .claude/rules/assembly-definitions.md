---
paths:
  - "game/Assets/_Project/Scripts/**/*.cs"
  - "game/Assets/_Project/Scripts/**/*.asmdef"
---

# Assembly Definition 경계 (docs/project-structure.md §3 기준)

```text
MonkeyLab.Core          — 순수 데이터, 시간, 사건 인터페이스
MonkeyLab.Gameplay      — 미션, 괴물, 감염, 투표 규칙
MonkeyLab.Network       — NGO와 MPS 의존 코드
MonkeyLab.Presentation  — Unity UI, 카메라, 오디오, VFX
MonkeyLab.Tests.EditMode
MonkeyLab.Tests.PlayMode
```

- 초기부터 지나치게 나누지는 않되 위 경계는 지킨다.
- 테스트 어셈블리는 필요한 런타임 어셈블리만 참조한다.
- **순환 참조를 만들지 않는다.** `Gameplay`가 `Presentation`을 참조하지 않는다.
- 새 스크립트를 만들 때 이 경계 중 어디에 속하는지 먼저 판단하고, 애매하면 구현 전에 질문한다.
- Assembly Definition 관련 변경은 `architect-reviewer` 에이전트로 교차 확인한다.
