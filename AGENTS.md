# AGENTS.md

> 이 파일은 Claude Code, Codex 등 여러 AI 코딩 도구가 공통으로 읽는 원본 규칙 파일이다.
> Claude Code는 `CLAUDE.md`에서 이 파일을 `@AGENTS.md`로 불러온다.

## 프로젝트 개요
- 원숭이 괴물 탈출 게임 (비대칭 소셜 디덕션 + 협동 탈출)
- 엔진: Unity 6.3 LTS URP / 언어: C#
- 6명 고정 (생존자 5 + 빌런 1), 라운드 15분
- 저장소: https://github.com/junn0s/escape-ukikki

## 문서 우선순위 (docs/README.md 기준 — 규칙 충돌 시 이 순서로 판단)
1. `docs/game-design-document.md` — 의도, 역할, 핵심 규칙, 승패
2. `docs/system-design-document.md` — 상세 상태, 예외 처리
3. `docs/technical-design-document.md` — Unity/네트워크 구현 방식
4. `docs/balance-and-telemetry.md` — 초기 수치, 조정 기준
5. 나머지 제작 문서 (`docs/mvp-scope.md`, `docs/map-level-design.md`, `docs/ui-ux-design.md`,
   `docs/art-audio-asset-guide.md`, `docs/project-structure.md`, `docs/qa-and-playtest-plan.md`,
   `docs/production-roadmap.md`)

## 무엇을 바꿔도 되고, 무엇은 안 되는가 (game-design-document.md 기준)
- **자유롭게 변경 가능**: 개별 밸런스 수치 (강화 단계 수치, 쿨타임, 감염 제한시간 등) — 플레이 테스트로 조정
- **GDD 버전을 올려야만 변경 가능**: 팀 구성(6명/5+1), 핵심 루프, 역할 정의, 승리 구조
- 코드를 짜다가 "이 값이 이상한데?" 싶으면, 밸런스 수치인지 핵심 규칙인지부터 구분해서 판단할 것

## 바이브코딩 중 기획 변경 대응
코드를 작성/수정하는 과정에서 기획서와 다르게 구현하는 것이 더 낫다고 판단되거나,
대화 중 기획 변경 지시를 받으면:

1. 그 변경이 "밸런스 수치"인지 "핵심 규칙"(역할/승패/루프)인지 먼저 판단해서 사용자에게 확인한다.
2. 밸런스 수치 변경이면: `docs/balance-and-telemetry.md`와 해당 ScriptableObject 값을 함께 수정한다.
   코드만 고치고 문서를 안 고치는 채로 넘어가지 않는다.
3. 핵심 규칙 변경이면: 코드를 먼저 짜지 말고, `docs/game-design-document.md`의 해당 항목과 GDD 버전을
   먼저 업데이트할지 사용자에게 확인한다. 승인 후에만 하위 문서와 코드를 반영한다.
4. 문서에 없는 임의의 값이나 규칙을 추측해서 코드에 넣지 않는다. 판단이 필요한 지점이면
   구현을 멈추고 먼저 질문한다.
5. 반영이 끝나면 `docs/devlog.md`에 무엇을, 왜 바꿨는지 한 줄로 기록한다.

## 문서-코드 연결 규칙 (project-structure.md §12 기준)
- GDD의 시스템 이름과 코드 서비스 이름을 최대한 일치시킨다.
- 밸런스 표의 키를 ScriptableObject 필드 이름과 매핑한다.
  (예: GDD "스피커 쿨타임 45초" → SO field `SpeakerCooldownSeconds`)
- 규칙이 바뀌면 **문서 → 데이터(ScriptableObject) → 테스트 → 코드** 순서로 반영한다.

## 파일명 규칙 (project-structure.md §6 기준)
- 문서 파일명에 "최종", "진짜최종", "수정본" 등을 붙이지 않는다 — Git 이력과 문서 버전(v1.0 등)으로 관리한다.
- 코드/에셋 네이밍은 `docs/project-structure.md` §6 표를 따른다 (Prefab `P_`, ScriptableObject `SO_` 등).

## 코드·구조 세부 규칙 (모든 도구 공통)
> 아래 내용은 `.claude/rules/`에도 동일하게 들어있다. `.claude/rules/`는 Claude Code가 관련 경로의
> 파일을 열 때 자동으로 다시 불러오는 편의 기능일 뿐, 규칙 자체는 여기(AGENTS.md)가 원본이다.
> 규칙을 바꿀 때는 이 절과 `.claude/rules/`의 해당 파일을 함께 수정한다.

### C# 코드 스타일 (docs/project-structure.md §7 기준)
- `Update`에서 매 프레임 검색 API를 호출하지 않는다.
- `Find`, `FindObjectOfType`, 문자열 기반 `GetComponent`를 런타임 핵심 경로에서 사용하지 않는다.
- Inspector 의존 필드는 `[SerializeField] private`로 선언하고, 필수 참조는 `Awake`에서 확인한다.
- 이벤트 구독은 활성화·비활성화 또는 생성·해제 수명주기를 맞춘다.
- 네트워크 콜백에서 UI를 직접 찾지 않고 Presenter에 전달한다.
- 매직 넘버는 Balance ScriptableObject 또는 명명된 상수로 이동한다.
- 예외를 빈 `catch`로 숨기지 않는다.
- 네이밍: 클래스/메서드 `PascalCase`, 지역변수/매개변수 `camelCase`, private 필드 `_camelCase`,
  인터페이스 `IName`, 비동기 메서드 `Async` 접미사, bool은 `is/has/can/should`로 표현.
- 네임스페이스: `MonkeyLab.Core`, `MonkeyLab.Gameplay.Missions`, `MonkeyLab.Gameplay.Monsters`,
  `MonkeyLab.Gameplay.Infection`, `MonkeyLab.Network`, `MonkeyLab.Presentation.UI`

### 폴더·에셋 네이밍 (docs/project-structure.md §2, §5, §6 기준)
- 프로젝트 소유 파일은 `_Project` 안에 두고, 외부 패키지를 임의로 수정해 복사해 넣지 않는다.
- 에셋 접두사: Prefab `P_`, ScriptableObject `SO_`, Material `M_`, Texture `T_`,
  Static Mesh `SM_`, Skinned Mesh `SK_`, Animation `A_`, Animator `AC_`,
  Audio `SFX_`/`AMB_`/`MUS_`, VFX `VFX_`, UI `UI_`
- 파일명에 공백, 한글, 괄호, "final", 버전 번호를 넣지 않는다.
- 씬 명명: `00_Bootstrap`, `01_MainMenu`, `02_Lobby`, `10_Laboratory`,
  `90_ArtSandbox`, `91_GameplaySandbox`

### Assembly Definition 경계 (docs/project-structure.md §3 기준)
```text
MonkeyLab.Core          — 순수 데이터, 시간, 사건 인터페이스
MonkeyLab.Gameplay      — 미션, 괴물, 감염, 투표 규칙
MonkeyLab.Network       — NGO와 MPS 의존 코드
MonkeyLab.Presentation  — Unity UI, 카메라, 오디오, VFX
```
- **순환 참조를 만들지 않는다.** `Gameplay`가 `Presentation`을 참조하지 않는다.
- 새 스크립트가 이 경계 중 어디에 속하는지 애매하면, 구현 전에 먼저 질문한다.

### 네트워크·서버 판정 규칙
- 승패, 감염, 미션 완료, 투표 결과 등 판정 로직은 반드시 서버(호스트) 권위로 처리하고,
  클라이언트만 실행하는 방식으로 구현하지 않는다.
- 클라이언트 입력은 서버에서 항상 재검증한다 (상호작용 거리, 쿨타임 등).
- 역할(생존자/빌런) 정보는 본인 화면에만 전송한다. 브로드캐스트로 전체 클라이언트에 보내지 않는다.
- 네트워크 동기화 로직 완료 조건에는 "Host+Client 최소 2개 인스턴스 테스트"를 포함한다.

### ScriptableObject 데이터 규칙 (docs/project-structure.md §8, §12 기준)
- ScriptableObject는 설정 원본으로 쓰고, 런타임 상태를 직접 저장하지 않는다.
- 밸런스 에셋은 `Default` / `Playtest` / `Demo` 프로필을 분리할 수 있다.
- GDD/밸런스 문서의 키와 ScriptableObject 필드 이름을 일치시킨다.
  ```text
  GDD: 스피커 쿨타임 45초
  Balance key: speakerCooldownSeconds
  SO field: SpeakerCooldownSeconds
  ```
- 밸런스 수치를 코드에 하드코딩하지 않는다.

## 개발 기록
- 매 작업 세션이 끝날 때, `docs/devlog.md` 맨 아래에 오늘 날짜로 구현 내용·기획서와 달라진 점·
  다음 할 일을 3줄 내외로 추가한다.
- 장황하게 쓰지 않는다. 상세 내용은 커밋 메시지나 관련 설계서에 남긴다.

## Git 커밋 규칙
- 커밋 메시지, PR 설명, 코드 주석 등 Git으로 관리되는 어떤 텍스트에도 "Claude", "AI",
  "Generated with" 등 AI 협업 흔적을 남기지 않는다.
- 커밋은 사람이 직접 작성한 것처럼 프로젝트 컨벤션(project-structure.md §10.4 브랜치,
  §10.5 커밋 메시지, 작업 단위 커밋)에 맞춰 작성한다.
- **한글 접두사를 쓴다**: `기능:`, `수정:`, `문서:`, `정리:`.
  `feat:`, `fix:`, `chore:` 같은 영문 Conventional Commits는 쓰지 않는다.
  (초기 커밋 일부는 영문 접두사를 쓰지만, 현재 컨벤션은 한글이다.)
- **기본은 `main` 직접 커밋이다.** 같은 씬·프리팹을 두 명이 동시에 수정할 때만 브랜치를 판다.
  씬과 프리팹은 병합 충돌 해결이 어려우므로, 브랜치를 팠다면 되도록 빨리 `main`에 합친다.
- (Claude Code 한정) 이 규칙은 `.claude/settings.json`의 `attribution` 설정과 함께 적용된다.
  다른 도구를 쓰는 경우에도 이 문서의 규칙을 동일하게 따른다.

## 도구별 확장 기능 안내 (Claude Code / Codex 팀원 모두 읽을 것)

**Skills (`.claude/skills/`)** — 5개 (monkey-fsm-check, balance-scriptableobject-sync,
mission-assignment-balance, villain-clue-consistency, game-doc-sync). 오픈 표준(Agent Skills)을
따르고 있어 Codex 등 다른 도구에서도 지원한다고 알려져 있으나, 정확한 설치 경로는 도구마다
다를 수 있으니 실제로 사용해보고 확인한다.

**서브에이전트 (`.claude/agents/`)** — 7개. **Claude Code 전용 형식(Markdown)**이라 Codex에서는
그대로 못 읽는다. Codex는 자체 서브에이전트 기능이 있지만 TOML 형식(`~/.codex/agents/`)을 쓰므로,
Codex 팀원이 동일한 역할 분담을 쓰고 싶다면 아래 표를 참고해 별도로 TOML 파일을 만들어야 한다.

| 에이전트 | 용도 |
|---|---|
| `game-developer` | AI 상태머신, 미션, 네트워크 동기화 등 게임플레이 구현 |
| `code-reviewer` | project-structure.md §13 리뷰 체크리스트 기반 코드 리뷰 |
| `debugger` | 네트워크 동기화 등 재현 어려운 버그의 근본 원인 분석 |
| `qa-expert` | qa-and-playtest-plan.md 기준 테스트 전략 |
| `performance-engineer` | 괴물 8마리 동시 상태 등 성능 목표 대응 |
| `architect-reviewer` | Assembly Definition 경계·순환 참조 검증 |
| `git-workflow-manager` | 브랜치 전략(main/feature/fix), 커밋 컨벤션 |

**규칙 (`.claude/rules/`)** — Claude Code 전용 자동 로딩 기능. 실제 내용은 위
"코드·구조 세부 규칙" 절에 그대로 들어있으므로, Codex 팀원은 이 문서만 읽으면 된다.

## 참고 문서
@docs/README.md