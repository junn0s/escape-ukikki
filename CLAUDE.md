@AGENTS.md

## Claude Code 전용 설정

프로젝트 규칙과 문서 우선순위는 위 `AGENTS.md`를 따른다 (Codex 등 다른 도구와 공유하는 원본).
아래는 Claude Code에서만 동작하는 확장 설정이다.

| 위치 | 동작 방식 |
| --- | --- |
| `.claude/rules/` | 해당 경로의 파일을 다룰 때 자동 적용 (C# 스타일, 폴더 구조, Assembly Definition, 네트워크 보안, SO 데이터, 설계 문서) |
| `.claude/skills/` | 반복 검증 절차. 자동 제안되거나 `/스킬이름`으로 호출 |
| `.claude/agents/` | 역할별 서브에이전트 (아래 표) |
| `.claude/settings.json` | 커밋/PR에 AI 협업 흔적(attribution) 자동 첨부 차단 |

### 서브에이전트

VoltAgent/awesome-claude-code-subagents 전체 목록(154개)에서 이 프로젝트에 필요한 7개만
선별·수정한 것이다.

| 에이전트 | 용도 |
| --- | --- |
| `game-developer` | AI 상태머신, 미션, 네트워크 동기화 등 게임플레이 구현 |
| `code-reviewer` | project-structure.md §13 리뷰 체크리스트 기반 코드 리뷰 |
| `debugger` | 네트워크 동기화 등 재현 어려운 버그의 근본 원인 분석 |
| `qa-expert` | qa-and-playtest-plan.md 기준 테스트 전략 |
| `performance-engineer` | 괴물 8마리 동시 상태 등 성능 목표 대응 |
| `architect-reviewer` | Assembly Definition 경계·순환 참조 검증 |
| `git-workflow-manager` | 브랜치 전략(main/feature/fix), 커밋 컨벤션 |

각 rule/skill 파일의 적용 조건은 파일 상단 frontmatter와 `description`에 정의되어 있다.
