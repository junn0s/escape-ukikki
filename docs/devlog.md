# 개발 기록 (devlog.md)

> 목적: 실제 개발 세션에서 있었던 결정·변경 사항을 짧게 남긴다.
> `production-roadmap.md`(계획서 — 앞으로 무엇을 할 것인가)와 달리, 이 문서는
> "실제로 무엇을 했는가"만 사후에 기록한다.
>
> 작성 규칙은 `.claude/skills/game-doc-sync/SKILL.md`와 `AGENTS.md`의 "개발 기록" 절을 따른다.
> 항목당 3줄 내외로 간결하게 유지한다.

---

<!-- 아래부터 실제 기록을 추가하세요 -->

## 2026-07-29

- 문서 점검 중 발견한 불일치 2건 수정. 기획 규칙 변경은 없고 서술만 정정했다.
- `project-structure.md` §1 트리 루트를 `NHN-Hackathon/` → `escape-ukikki/`로, §1 경로 주의사항을
  실제 저장소 기준(경로에 한글·공백 없음, 상위 경로 대비 따옴표 유지)으로 바꿨다.
- `map-level-design.md` §2에 격리 제어 패널이 별도 방이 아니라는 GDD §13.3 근거를 명시했다. 방 10개는 그대로다.

## 2026-07-29 (2)

- 시각 방향을 어몽어스 계열 데포르메로 확정. 직원 3등신(높이 1.6m, 휴머노이드 스켈레톤 유지),
  괴물은 의도적으로 데포르메하지 않아 톤 대비를 만든다. `art-audio-asset-guide.md` §1.4 신설, §2.1·§3.1 수정.
- 등신을 2가 아닌 3으로 둔 이유는 쿼터뷰(50~60도)에서 2등신은 머리에 몸통이 가리고, 기성 애니메이션
  리타게팅이 깨져 12종을 직접 제작해야 하기 때문이다. 직원 폴리곤 목표는 10k~25k → 5k~12k로 하향.
- 캐릭터 높이를 1.6m로 유지해 맵 공간 수치는 변경 없음. GDD §2 시각 방향 한 줄, 맵 문서 §3 주석,
  QA §4.3 검증 항목 3개를 함께 갱신했다. 다음 할 일: M0 착수.

## 2026-07-29 (3) — M0 프로젝트 기반

- Unity 6000.3.20f1로 `game/` 생성. URP·Input System·NGO 2.5.1·Multiplayer Services·AI Navigation
  패키지 추가. `-createProject`는 URP 파이프라인 에셋을 만들지 않아 `URP_Laboratory.asset`을 직접
  생성해 Graphics/Quality에 연결했다.
- 폴더 구조(project-structure.md §2), asmdef 6개(Core/Gameplay/Network/Presentation + 테스트 2),
  씬 6개(Build Settings 등록, 90·91은 비활성), `SO_GameBalance` + Default 에셋을 만들었다.
  밸런스 필드명은 balance-and-telemetry.md 키와 1:1로 맞췄다.
- EditMode 테스트 7개 작성·통과. 문서 수치를 코드로 고정해, 밸런스를 바꾸면 테스트가 먼저 깨진다.
- 미완: Windows Build Support 모듈 미설치로 "빈 Windows Development Build 실행" 완료 조건은
  아직 검증하지 못했다. `docs/build-checklist.md` §5에 절차만 남겼다. 다음 할 일: 모듈 설치 후
  빌드 검증, 이어서 M1(로컬 버티컬 슬라이스).

## 2026-07-29 (4) — M1 로컬 버티컬 슬라이스

- 첫 플레이 가능 프로토타입. 91_GameplaySandbox 씬에 방 3개 그레이박스, 플레이어(3등신 임시 메시),
  괴물 1마리, 퓨즈 미션 스테이션을 배치했다. 이동 → 미션 실패 → 소음 → 괴물 조사 → 추격 →
  물기 → 감염 타이머의 M1 흐름이 로컬에서 이어진다.
- 소음 우선순위(SDD §9.2 5단계), 감염 규칙(GDD §14.1), 퓨즈 로직을 Core에 순수 클래스로 두고
  EditMode 테스트 33개로 고정했다. 특히 완전 동점 시 NoiseId 비교는 결정성 요구라 순서 무관
  테스트를 따로 뒀다.
- 문서와 달라진 점 2건. (1) `Scripts/` 하위 게임플레이 폴더를 `Domain/` 안으로 옮겼다.
  asmdef는 자기 폴더 트리에만 적용되어 기존 배치로는 어셈블리 경계가 강제되지 않았다.
  project-structure.md §2를 함께 수정했다. (2) NavMesh 베이크 결과를 씬에 두지 않고
  `Data/Maps/NavMesh_GameplaySandbox.asset`으로 분리했다. 씬에 남기면 바이너리 데이터가 섞여
  씬 전체가 바이너리로 저장되고 §10.3의 "씬은 텍스트" 규칙이 깨진다.
- 다음 할 일: 에디터에서 실제 플레이해 카메라 가독성과 3등신 비율 확인(M1 완료 조건),
  이후 M2 네트워크 기반.
