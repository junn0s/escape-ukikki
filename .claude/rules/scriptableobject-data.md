---
paths:
  - "game/Assets/_Project/Data/**/*"
  - "game/Assets/_Project/Scripts/**/*.cs"
---

# ScriptableObject 데이터 규칙 (docs/project-structure.md §8, §12 기준)

- ScriptableObject는 설정 원본으로 사용한다. 런타임 상태를 직접 저장하지 않는다.
- 에셋마다 안정적인 문자열 ID를 둔다.
- 프리팹과 아이콘 참조는 카탈로그 ScriptableObject에 모은다.
- 밸런스 에셋은 `Default`, `Playtest`, `Demo` 프로필을 분리할 수 있다. 빌드에 어떤 프로필을
  쓰는지 Bootstrap 설정에 명시한다.
- GDD/밸런스 문서의 키와 ScriptableObject 필드 이름을 일치시킨다.

예:
```text
GDD: 스피커 쿨타임 45초
Balance key: speakerCooldownSeconds
SO field: SpeakerCooldownSeconds
Test: SpeakerCooldown_BlocksUseBefore45Seconds
```

- 밸런스 수치를 코드에 하드코딩하지 않는다. 항상 ScriptableObject 필드를 통해 참조한다.
- 밸런스 값을 바꿀 때는 `docs/balance-and-telemetry.md`도 함께 갱신한다 (AGENTS.md "바이브코딩 중
  기획 변경 대응" 절차 참조).
