# Unity 프로젝트 실행 안내

> 대상: `feature/m0-m1-foundation` 브랜치
> 이 문서는 Unity 프로젝트(`game/`)를 열고 프로토타입을 실행하는 방법만 다룬다.
> 게임 기획과 규칙은 [`docs/`](../docs/README.md)를 본다.

---

## 1. 필요한 것

| 항목 | 버전 | 비고 |
| --- | --- | --- |
| Unity | **6000.3.20f1** | 다른 버전으로 열면 `ProjectVersion.txt`가 바뀌어 충돌이 생긴다 |
| Git LFS | 3.x | FBX·PNG·WAV를 받으려면 필요 |
| 플랫폼 | macOS / Windows | Windows 빌드는 별도 모듈 필요 (§6) |

### 처음 받는 경우

```bash
git clone https://github.com/junn0s/escape-ukikki.git
cd escape-ukikki
git lfs install
git lfs pull
```

Unity Hub → Add → `escape-ukikki/game` 폴더를 선택한다. 저장소 루트가 아니라 **`game/`** 이다.

첫 실행은 패키지 임포트와 셰이더 컴파일로 몇 분 걸린다. `game/Library/`가 생기지만 Git에는
올라가지 않는다.

---

## 2. 프로토타입 실행

1. Unity에서 프로젝트를 연다.
2. Project 창에서 `Assets/_Project/Scenes/91_GameplaySandbox.unity`를 더블클릭한다.
3. Play 버튼을 누른다.

### 조작

| 입력 | 동작 |
| --- | --- |
| WASD | 이동 (화면 방향 기준) |
| 마우스 | 바라보는 방향 |
| E | 상호작용 / 퓨즈 삽입 |

### 확인할 수 있는 흐름

```text
퓨즈 스테이션에 접근 → E로 미션 시작 → E 반복 입력
  → 20% 확률로 실패 → Medium 소음(반경 14m) 발생
  → 괴물이 소리를 듣고 1.5배 속도로 달려옴
  → 시야(7m) 또는 후각(0.5m)에 걸리면 추격
  → 물리면 감염, 90초 타이머 시작
```

화면 좌측 상단에 괴물 상태, 감염 남은 시간, 상호작용 프롬프트가 표시된다
(개발 빌드 전용 임시 HUD).

Scene 뷰에서 괴물을 선택하면 시야 부채꼴과 후각 반경이 기즈모로 보인다.

---

## 3. 씬 구성

| 씬 | 상태 |
| --- | --- |
| `00_Bootstrap` | 빈 씬 (서비스 초기화 예정) |
| `01_MainMenu` | 빈 씬 |
| `02_Lobby` | 빈 씬 |
| `10_Laboratory` | 빈 씬 (10개 방 본 맵 예정) |
| `90_ArtSandbox` | 빈 씬 |
| `91_GameplaySandbox` | **M1 프로토타입. 여기서 플레이한다** |

`91_GameplaySandbox`는 방 3개(전력 복구실·실험실 A·백신실 A)와 연결 복도로 된 그레이박스다.

---

## 4. 테스트

게임 규칙은 Unity 없이 검증할 수 있게 순수 C#으로 분리해뒀다.

### 에디터에서

Window → General → Test Runner → EditMode → Run All

### 명령줄에서

```bash
# macOS
"/Applications/Unity/Hub/Editor/6000.3.20f1/Unity.app/Contents/MacOS/Unity" \
  -projectPath game -batchmode -runTests -testPlatform EditMode \
  -testResults /tmp/results.xml -nographics -logFile /tmp/unity-test.log
```

종료 코드 0이면 통과. 결과 상세는 `/tmp/results.xml`에 있다.

현재 33개가 통과한다. 무엇을 검증하는지는 아래와 같다.

| 테스트 파일 | 고정하는 규칙 |
| --- | --- |
| `GameBalanceDefaultsTests` | 밸런스 기본값이 `docs/balance-and-telemetry.md` 표와 일치 |
| `NoisePrioritySelectorTests` | 소음 우선순위 5단계 (SDD §9.2), 입력 순서와 무관한 결정성 |
| `InfectionStateTests` | 감염 재물림·치료·사망 규칙 (GDD §14.1) |
| `FusePuzzleTests` | 실패 시 진행 초기화 (GDD §10.1) |

**밸런스 수치를 바꾸면 테스트가 먼저 깨지는 것이 정상이다.** 테스트를 고치기 전에
`docs/balance-and-telemetry.md`부터 고쳤는지 확인한다 (문서 → 데이터 → 테스트 → 코드).

---

## 5. 코드 구조

```text
game/Assets/_Project/Scripts/
├── Core/      → MonkeyLab.Core        순수 로직, Unity 비의존
├── Domain/    → MonkeyLab.Gameplay    미션·괴물·감염·소음 규칙
├── Network/   → MonkeyLab.Network     (M2에서 사용)
└── UI/        → MonkeyLab.Presentation 카메라·HUD
```

Assembly Definition은 **자기 폴더 트리에만** 적용된다. 새 게임플레이 스크립트는
`Domain/` 하위에 둬야 `MonkeyLab.Gameplay`에 포함된다.

`Gameplay`는 `Presentation`을 참조하지 않는다 (`docs/project-structure.md` §3).

### 밸런스 값

수치는 코드에 하드코딩하지 않고 `Assets/_Project/Data/Balance/SO_GameBalance_Default.asset`에서
읽는다. 필드 이름은 `docs/balance-and-telemetry.md`의 키와 1:1로 맞춰져 있다.

---

## 6. Windows 빌드

**아직 검증되지 않았다.** Windows Build Support 모듈이 설치돼 있지 않다.

1. Unity Hub → Installs → 6000.3.20f1 → 톱니바퀴 → Add modules
2. **Windows Build Support (Mono)** 체크 후 설치
3. `docs/build-checklist.md` §5의 절차를 따른다

---

## 7. 씬을 다시 만들어야 할 때

`91_GameplaySandbox`는 코드로 생성했다. 씬이 깨지거나 맵 수치를 바꾸려면
`Assets/Editor/M1SceneBuilder.cs`를 수정한 뒤 다시 생성한다.

```bash
"/Applications/Unity/Hub/Editor/6000.3.20f1/Unity.app/Contents/MacOS/Unity" \
  -projectPath game -batchmode -quit -nographics \
  -executeMethod MonkeyLab.EditorTools.M1SceneBuilder.Run \
  -logFile /tmp/scene.log
```

기존 씬을 덮어쓰므로 에디터에서 직접 수정한 내용은 사라진다.

---

## 8. 주의할 점

### NavMesh는 씬이 아니라 에셋에 있다

`Assets/_Project/Data/Maps/NavMesh_GameplaySandbox.asset`. 씬에 그대로 두면 바이너리 데이터가
섞여 씬 전체가 바이너리로 저장되고, 그러면 씬 병합이 불가능해진다.

### 씬은 반드시 텍스트여야 한다

Project Settings → Editor → Asset Serialization = **Force Text**.
`Assets/Editor/M0ProjectSettings.cs`를 실행하면 다시 맞출 수 있다.

### 커밋 전 확인

`docs/build-checklist.md` §2를 따른다. 요약하면:

- `Library/`, `Temp/`, `UserSettings/`가 스테이징에 없을 것
- 새 에셋의 `.meta`가 함께 올라갈 것
- 씬·프리팹·머티리얼은 LFS가 아닌 일반 Git으로 들어갈 것

---

## 9. 이 브랜치의 범위

M0(프로젝트 기반)과 M1(로컬 버티컬 슬라이스)까지다.

**되는 것**: 로컬 1인 플레이, 이동, 퓨즈 미션, 소음, 괴물 순찰·조사·추격·물기, 감염 타이머

**안 되는 것**: 네트워크 멀티플레이, 역할 배정, 회의·투표, 해독제, 빌런 능력, 승패 판정

> **참고**: 이 브랜치는 `main`이 12커밋 앞서기 전 시점에서 갈라져 나왔다. `main`에도 같은
> 영역(괴물 AI, 소음, 퓨즈, 감염)의 구현이 있으므로, 병합 전에 어느 구현을 살릴지
> 팀에서 먼저 정해야 한다. 겹치는 파일은 `MonsterBrain`, `MonsterSenses`,
> `MonsterBiteController`, `NoiseService`, `IInteractable`, `Player*`,
> `QuarterViewCamera`와 `91_GameplaySandbox.unity`다.
