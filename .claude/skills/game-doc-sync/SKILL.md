---
name: game-doc-sync
description: "코드를 수정한 뒤, 관련 기획/설계 문서(GDD, SDD, 밸런스 문서)와 실제 구현이 어긋나지 않는지 대조할 때 사용한다."
---

## Purpose
코드 변경 후 문서와 구현의 정합성을 대조하고, 어긋난 부분을 표면화한다.

## Use When
- 규칙이나 수치에 영향을 주는 코드 변경을 마쳤을 때
- 세션을 마무리하기 전, 문서 갱신 여부를 최종 확인할 때

## Process
1. 이번 세션에서 변경한 코드가 참조하는 문서(GDD/SDD/TDD/밸런스 문서)를 특정한다.
2. 문서 우선순위(AGENTS.md 참조)에 따라, 상위 문서부터 실제 구현과 대조한다.
3. 어긋난 부분이 있으면:
   - 밸런스 수치 문제 → `balance-scriptableobject-sync` 스킬로 처리
   - 핵심 규칙 문제 → AGENTS.md "바이브코딩 중 기획 변경 대응" 절차로 사용자에게 확인
4. 확인이 끝나면 `docs/devlog.md`에 오늘 날짜로 아래 형식 3줄을 추가한다.
   ```markdown
   ## YYYY-MM-DD
   - 구현: (오늘 한 일)
   - 기획서와 달라진 점: (없음 / 구체적으로)
   - 다음 할 일: (다음 세션에서 이어갈 것)
   ```

## Outputs
- `docs/devlog.md`에 추가된 기록 항목
- 발견된 불일치 목록 (있는 경우)

## Quality Bar
- devlog 기록은 3줄 내외로 간결하게 유지한다. 상세 내용은 커밋 메시지나 설계서에 남긴다.

## Related Skills
- `balance-scriptableobject-sync`, `mission-assignment-balance`, `villain-clue-consistency`
