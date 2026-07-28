---
paths:
  - "game/Assets/_Project/Scripts/Network/**/*.cs"
  - "game/Assets/_Project/Scripts/Gameplay/**/*.cs"
---

# 네트워크·서버 판정 규칙

- 승패, 감염, 미션 완료, 투표 결과 등 **판정에 관련된 로직은 반드시 서버(호스트) 권위로 처리**하고,
  클라이언트만 실행하는 방식으로 구현하지 않는다. (docs/project-structure.md §13 리뷰 체크리스트 항목)
- 클라이언트 입력은 서버에서 항상 재검증한다 (상호작용 거리, 쿨타임 등).
- 역할(생존자/빌런) 정보는 본인 화면에만 전송한다. 브로드캐스트로 전체 클라이언트에 보내지 않는다.
- 네트워크 콜백에서 UI를 직접 조작하지 않고 Presenter를 거친다 (coding-style.md와 동일 원칙).
- 새로 네트워크 동기화 로직을 작성하면, 완료 조건으로 "Host+Client 최소 2개 인스턴스 테스트"를
  포함한다 (production-roadmap.md 완료 조건 패턴).
- 이 영역의 버그는 `debugger` 에이전트, 구조적 검토는 `architect-reviewer` 에이전트를 활용한다.
