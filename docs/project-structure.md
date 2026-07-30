# 프로젝트 및 파일 구조

> 문서 버전: 1.1
> 목적: Unity 프로젝트와 제작 자료를 충돌 없이 관리하기 위한 디렉터리·명명·Git 규칙

---

## 1. 저장소 구조

```text
NHN-Hackathon/
├── docs/
│   ├── README.md
│   ├── game-design-document.md
│   ├── mvp-scope.md
│   ├── system-design-document.md
│   ├── map-level-design.md
│   ├── ui-ux-design.md
│   ├── art-audio-asset-guide.md
│   ├── technical-design-document.md
│   ├── project-structure.md
│   ├── balance-and-telemetry.md
│   ├── qa-and-playtest-plan.md
│   └── production-roadmap.md
├── game/
│   ├── Assets/
│   ├── Packages/
│   ├── ProjectSettings/
│   └── UserSettings/             # Git 제외
├── source-assets/
│   ├── Blender/
│   ├── Textures/
│   ├── Audio/
│   └── UI/
├── tools/
├── builds/                       # Git 제외
├── .editorconfig
├── .gitattributes
├── .gitignore
├── README.md
└── THIRD_PARTY_NOTICES.md
```

Unity 프로젝트 디렉터리는 짧은 영문 `game`을 사용한다. 현재 상위 경로에 한글과 공백이 있으므로 모든 자동화 스크립트는 경로를 반드시 따옴표로 감싼다.

---

## 2. Unity Assets 구조

```text
game/Assets/
├── _Project/
│   ├── Art/
│   │   ├── Characters/
│   │   ├── Monsters/
│   │   ├── Environment/
│   │   ├── Props/
│   │   ├── Materials/
│   │   ├── Textures/
│   │   ├── Animations/
│   │   └── VFX/
│   ├── Audio/
│   │   ├── Music/
│   │   ├── Ambience/
│   │   ├── SFX/
│   │   └── Mixers/
│   ├── Data/
│   │   ├── Balance/
│   │   ├── Missions/
│   │   ├── Maps/
│   │   └── Catalogs/
│   ├── Prefabs/
│   │   ├── Core/
│   │   ├── Players/
│   │   ├── Monsters/
│   │   ├── Environment/
│   │   ├── Missions/
│   │   ├── Interactables/
│   │   ├── Network/
│   │   └── UI/
│   ├── Scenes/
│   ├── Scripts/
│   │   ├── Core/
│   │   │   └── Utilities/
│   │   ├── Gameplay/
│   │   │   ├── Domain/
│   │   │   ├── Application/
│   │   │   ├── Player/
│   │   │   ├── Monsters/
│   │   │   ├── Missions/
│   │   │   ├── Infection/
│   │   │   ├── Villain/
│   │   │   └── Meeting/
│   │   ├── Network/
│   │   ├── Presentation/
│   │   │   ├── UI/
│   │   │   ├── Camera/
│   │   │   ├── Audio/
│   │   │   └── VFX/
│   │   └── Editor/
│   ├── Settings/
│   ├── Shaders/
│   ├── UI/
│   │   ├── Fonts/
│   │   ├── Icons/
│   │   ├── Sprites/
│   │   └── Themes/
│   └── Tests/
│       ├── EditMode/
│       └── PlayMode/
├── ThirdParty/
└── Plugins/
```

프로젝트 소유 파일은 `_Project` 안에 둔다. 외부 패키지를 임의로 수정해 `_Project` 안으로 복사하지 않는다.

---

## 3. Assembly Definition

초기부터 지나치게 나누지는 않되 다음 경계는 권장한다.

```text
MonkeyLab.Core
MonkeyLab.Gameplay
MonkeyLab.Network
MonkeyLab.Presentation
MonkeyLab.Tests.EditMode
MonkeyLab.Tests.PlayMode
```

- `Core`: 순수 데이터, 시간, 사건 인터페이스
- `Gameplay`: 미션, 괴물, 감염, 투표 규칙
- `Network`: NGO와 MPS 의존 코드
- `Presentation`: Unity UI, 카메라, 오디오, VFX
- 테스트 어셈블리는 필요한 런타임 어셈블리만 참조

순환 참조를 만들지 않는다. `Gameplay`이 `Presentation`을 참조하지 않는다.

---

## 4. 씬 명명과 소유

| 파일 | 역할 |
| --- | --- |
| `00_Bootstrap.unity` | 영구 서비스 |
| `01_MainMenu.unity` | 메인 메뉴 |
| `02_Lobby.unity` | 로비 |
| `10_Laboratory.unity` | 라운드 맵 |
| `90_ArtSandbox.unity` | 아트 테스트 |
| `91_GameplaySandbox.unity` | 시스템 테스트 |

한 사람이 장시간 메인 맵 씬을 잠그지 않도록 방과 시스템을 프리팹으로 분리한다. 큰 씬 변경 전 팀에 알리고 작업 시간을 조율한다.

---

## 5. 프리팹 구성

### 5.1 방

```text
P_Room_LabA
├── Geometry
├── Collision
├── Occlusion
├── Lighting
├── Props
├── MissionAnchors
├── AudioZone
└── NavModifiers
```

### 5.2 상호작용 오브젝트

```text
P_MissionStation_Fuse
├── Visual
├── Collider
├── InteractionPoint
├── NetworkObject
├── MissionStationNetwork
├── HighlightPresenter
└── AudioSource
```

프리팹 루트 이름과 파일명을 일치시킨다. 씬에 프리팹을 풀어서 수정하지 않고 필요한 경우 Prefab Variant를 사용한다.

---

## 6. 파일 명명 규칙

### 6.1 코드

- 클래스·파일: `PascalCase`
- 메서드·프로퍼티: `PascalCase`
- 지역 변수·매개변수: `camelCase`
- private 필드: `_camelCase`
- 인터페이스: `IName`
- 열거형: 단수형 `PlayerLifeState`
- 비동기 메서드: `Async` 접미사
- bool: `is`, `has`, `can`, `should`로 의미 표현

클래스 파일은 주요 public 타입 하나만 가진다.

### 6.2 Unity 에셋

| 종류 | 규칙 | 예시 |
| --- | --- | --- |
| Prefab | `P_` | `P_Player`, `P_Room_Security` |
| ScriptableObject | `SO_` | `SO_GameBalance_Default` |
| Material | `M_` | `M_LabMetal_Blue` |
| Texture | `T_` | `T_LabMetal_BaseColor` |
| Sprite | `S_` | `S_Player_Body` |
| Sprite Atlas | `SA_` | `SA_Laboratory` |
| Static Mesh | `SM_` | `SM_LabDoor` |
| Skinned Mesh | `SK_` | `SK_Player` |
| Animation | `A_` | `A_Player_Interact` |
| Animator | `AC_` | `AC_Monkey` |
| Audio | `SFX_`, `AMB_`, `MUS_` | `SFX_Speaker_On_01` |
| VFX | `VFX_` | `VFX_VentSmoke_Red` |
| UI | `UI_` | `UI_Icon_Antidote` |

파일명에는 공백, 한글, 괄호, `final`, 버전 번호를 넣지 않는다.

---

## 7. C# 코드 규칙

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

### 7.1 네임스페이스

```text
MonkeyLab.Core
MonkeyLab.Gameplay.Missions
MonkeyLab.Gameplay.Monsters
MonkeyLab.Gameplay.Infection
MonkeyLab.Network
MonkeyLab.Presentation.UI
```

---

## 8. ScriptableObject 규칙

- 설정 원본으로 사용한다.
- 런타임 상태를 직접 저장하지 않는다.
- 에셋마다 안정적인 문자열 ID를 둔다.
- 프리팹과 아이콘 참조를 카탈로그에 모은다.
- 밸런스 에셋은 `Default`, `Playtest`, `Demo` 프로필을 분리할 수 있다.
- 빌드에 어떤 프로필을 쓰는지 Bootstrap 설정에 명시한다.

---

## 9. Resources와 로딩

- `Resources` 폴더 사용을 피한다.
- MVP는 씬·프리팹 직접 참조와 카탈로그 ScriptableObject를 사용한다.
- Addressables는 원격 콘텐츠와 다중 맵 필요가 생긴 뒤 도입한다.
- 런타임 문자열 경로로 에셋을 찾지 않는다.

---

## 10. Git 관리

### 10.1 커밋 대상

- `Assets`와 `.meta`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `ProjectSettings`
- 문서와 도구 스크립트

### 10.2 제외 대상

- `Library`
- `Temp`
- `Logs`
- `Obj`
- `Build`
- `Builds`
- `UserSettings`
- IDE 캐시

### 10.3 Git LFS 후보

- `*.fbx`
- `*.blend`
- `*.psd`
- `*.tga`
- `*.wav`
- 고용량 영상

Unity YAML 씬·프리팹·머티리얼은 일반 Git으로 관리한다. Unity의 Asset Serialization은 Force Text, Version Control은 Visible Meta Files로 설정한다.

### 10.4 브랜치

- `main`: 시연 가능한 안정 상태
- `feature/<short-name>`: 기능
- `fix/<short-name>`: 버그

해커톤 기간이 짧아도 작업 단위 커밋을 유지한다. 씬과 ProjectSettings 변경은 커밋 메시지에 명시한다.

---

## 11. 외부 패키지

- 패키지 이름, 버전, 출처와 라이선스를 기록한다.
- 사용하지 않는 샘플과 데모 씬을 제거한다.
- 외부 코드를 직접 수정해야 하면 래퍼 또는 별도 패치 파일로 변경 이유를 남긴다.
- 패키지 업그레이드는 기능 개발과 같은 커밋에 섞지 않는다.
- 해커톤 직전에는 패키지 버전을 올리지 않는다.

---

## 12. 문서와 코드 연결

- GDD의 시스템 이름과 코드 서비스 이름을 가능한 한 일치시킨다.
- 밸런스 표의 키를 ScriptableObject 필드 이름과 매핑한다.
- QA 케이스 ID를 테스트 이름 또는 이슈에 포함한다.
- 규칙 변경은 문서, 데이터, 테스트 순서로 반영한다.

예:

```text
GDD: 스피커 쿨타임 45초
Balance key: speakerCooldownSeconds
SO field: SpeakerCooldownSeconds
Test: SpeakerCooldown_BlocksUseBefore45Seconds
```

---

## 13. 리뷰 체크리스트

- [ ] 새 파일이 올바른 디렉터리에 있는가?
- [ ] `.meta`가 함께 추가됐는가?
- [ ] 공개 정보와 비밀 정보가 분리됐는가?
- [ ] 서버 판정이 필요한 로직을 클라이언트만 실행하지 않는가?
- [ ] 값이 코드에 중복 하드코딩되지 않았는가?
- [ ] 이벤트 구독이 해제되는가?
- [ ] 씬에 Missing Script 또는 Missing Prefab이 없는가?
- [ ] 외부 에셋 라이선스가 기록됐는가?
- [ ] 6인 테스트 또는 최소 Host+Client 테스트를 했는가?
- [ ] 관련 문서와 QA 항목을 갱신했는가?
