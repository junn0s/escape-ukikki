---
paths:
  - "game/Assets/_Project/Scripts/**/*.cs"
---

# C# 코드 규칙 (docs/project-structure.md §7 기준)

- `Update`에서 매 프레임 검색 API를 호출하지 않는다.
- `Find`, `FindObjectOfType`, 문자열 기반 `GetComponent`를 런타임 핵심 경로에서 사용하지 않는다.
- Inspector 의존 필드는 `[SerializeField] private`로 선언한다.
- 필수 참조는 `Awake` 또는 에디터 검증에서 확인한다.
- 공개 필드는 데이터 구조 목적 외에는 피한다.
- 이벤트 구독은 활성화·비활성화 또는 생성·해제 수명주기를 맞춘다.
- 네트워크 콜백에서 UI를 직접 찾지 않고 Presenter에 전달한다.
- 매직 넘버는 Balance ScriptableObject 또는 명명된 상수로 이동한다.
- 로그는 카테고리와 라운드·플레이어 ID를 포함한다.
- 예외를 빈 `catch`로 숨기지 않는다.

## 네이밍
- 클래스·파일: `PascalCase` / 메서드·프로퍼티: `PascalCase`
- 지역 변수·매개변수: `camelCase` / private 필드: `_camelCase`
- 인터페이스: `IName` / 열거형: 단수형 (예: `PlayerLifeState`)
- 비동기 메서드: `Async` 접미사
- bool: `is`, `has`, `can`, `should`로 의미 표현
- 클래스 파일은 주요 public 타입 하나만 가진다.

## 네임스페이스
```text
MonkeyLab.Core
MonkeyLab.Gameplay.Missions
MonkeyLab.Gameplay.Monsters
MonkeyLab.Gameplay.Infection
MonkeyLab.Network
MonkeyLab.Presentation.UI
```
