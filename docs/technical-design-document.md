# 기술 설계서

> 문서 버전: 1.17
> 기준 GDD: 1.5
> 대상: Unity 6.3 LTS, Windows PC, 6인 호스트 방식 온라인 게임

---

## 1. 기술 목표

- 6명이 참가 코드로 한 세션에 접속한다.
- 호스트가 게임 규칙, AI와 승패를 판정한다.
- 클라이언트에는 자기 역할과 개인 정보만 공개한다.
- 10개 방, 플레이어 6명, 괴물 최대 8마리를 기준 PC에서 60fps로 실행한다.
- 네트워크 지연과 중복 요청에도 미션, 아이템, 투표가 한 번만 처리된다.
- 씬·프리팹·데이터를 작은 팀이 충돌 없이 분담할 수 있다.
- 밸런스 값은 코드 수정 없이 조정할 수 있다.

---

## 2. 확정 기술 스택

| 영역 | 선택 |
| --- | --- |
| 엔진 | Unity 6.3 LTS |
| 언어 | C# |
| 렌더링 | Universal Render Pipeline |
| 입력 | Unity Input System |
| UI | uGUI + TextMeshPro |
| 월드 물리 | Unity 2D Physics (`Rigidbody2D`, `Collider2D`) |
| AI 이동 | 2D 웨이포인트 그래프 + 서버 경로 탐색 |
| 네트워크 | Netcode for GameObjects |
| 전송 계층 | Unity Transport |
| 세션 | Multiplayer Services SDK Sessions |
| 인터넷 연결 | Unity Relay |
| 인증 | Unity Authentication 익명 로그인 |
| 테스트 | Unity Test Framework + Multiplayer Play Mode |
| 버전 관리 | Git + Git LFS |
| 타깃 | Windows x64 |

Unity 6.3 LTS는 장기 지원 기준선으로 사용한다. Unity 공식 지원 페이지는 6.3 LTS를 2027년 12월까지 지원한다고 안내한다.

- [Unity 6 릴리스 지원](https://unity.com/releases/unity-6/support)
- [Unity Multiplayer Services SDK](https://docs.unity.com/en-us/relay/mirror)
- [Relay와 Netcode for GameObjects 연동](https://docs.unity.com/en-us/relay/relay-and-ngo)
- [Unity 캐주얼 협동 멀티플레이 권장 구성](https://docs.unity.com/en-us/multiplayer/game-types/co-op-games)
- [Unity 2D Physics](https://docs.unity3d.com/6000.0/Documentation/Manual/Physics2D.html)

새 프로젝트에서는 개별 Lobby·Relay 패키지 대신 통합 `Multiplayer Services` 패키지를 사용한다. 공식 문서에서 Unity 6의 단독 Relay 패키지는 통합 패키지로 대체되는 방향임을 명시하고 있다.

### 2.1 MVP에서 쓰지 않는 기술

- Netcode for Entities/DOTS
- 전용 게임 서버
- Addressables 원격 배포
- 외부 데이터베이스
- ECS 기반 AI
- FMOD/Wwise
- 커스텀 렌더 파이프라인
- 물리 기반 네트워크 예측 프레임워크

팀에 이미 검증된 경험이 없는 기술은 해커톤 도중 도입하지 않는다.

---

## 3. 상위 아키텍처

```text
Presentation
  UI / Camera / Animation / VFX / Audio
            ↓
Application
  Round orchestration / Use cases / Commands
            ↓
Gameplay Domain
  Roles / Missions / Noise / Monsters / Infection / Voting
            ↓
Infrastructure
  NGO / MPS Sessions / Relay / Unity scene / Persistence / Logs
```

### 3.1 규칙

- Gameplay Domain은 가능한 한 Unity UI와 에셋 로딩을 직접 참조하지 않는다.
- 네트워크 스크립트는 도메인 서비스를 호출하고 결과를 전송한다.
- UI는 복제된 읽기 모델을 표시하며 승패를 직접 결정하지 않는다.
- ScriptableObject는 정적 설정에만 사용하고 라운드 가변 상태를 저장하지 않는다.
- 전역 Singleton을 늘리지 않고 Bootstrap에서 필요한 서비스를 조립한다.

---

## 4. 씬 구조

| 씬 | 책임 |
| --- | --- |
| `00_Bootstrap` | 서비스 초기화, 인증, 전역 오브젝트 |
| `01_MainMenu` | 방 생성·참가, 설정 |
| `02_Lobby` | 참가자, 준비, 시작 |
| `10_Laboratory` | 실제 라운드 월드 |
| `90_ArtSandbox` | 아트·조명 검증 전용, 빌드 제외 가능 |
| `91_GameplaySandbox` | 미션·괴물 로컬 테스트, 릴리스 제외 |

`Bootstrap`은 `DontDestroyOnLoad`가 필요한 최소 오브젝트만 가진다. 라운드 시스템은 게임 씬 진입 시 생성하고 종료 시 정리한다.

### 4.1 부팅 흐름

1. 로컬 설정 로드
2. Unity Services 초기화
3. 익명 인증
4. 네트워크 상태 확인
5. 메인 메뉴 로드

서비스 초기화 실패 시 제한된 재시도와 사용자용 오류 메시지를 제공한다.

`BootstrapEntryPoint`는 Inspector에 연결된 `IBootstrapTask`를 순서대로 실행하고 모든 작업이
성공한 뒤에만 메인 메뉴를 연다. M2의 첫 작업은 `UnityServicesInitializer`이며 다음 순서를
보장한다.

1. 이미 초기화되지 않았다면 `UnityServices.InitializeAsync()` 실행
2. 이미 로그인하지 않았다면 `AuthenticationService.Instance.SignInAnonymouslyAsync()` 실행
3. 유효한 Player ID를 확인한 뒤 Ready 상태 확정

초기화와 로그인은 중복 호출되어도 같은 진행 작업을 공유한다. 실패하면 Bootstrap 씬에 머물고
사용자용 오류와 재시도 버튼을 표시한다. Unity Cloud 프로젝트가 연결되지 않은 경우에는 코드
오류로 숨기지 않고 Project Settings의 Services 연결이 필요하다고 안내한다.

익명 인증의 세션 토큰은 SDK의 로컬 캐시를 그대로 사용한다. 로그에는 액세스 토큰이나 세션
토큰을 남기지 않는다.

### 4.2 방 생성·참가 흐름

`01_MainMenu`의 `GameSessionController`는 `GameSessionService`를 통해 MPS Session을 생성하거나
참가 코드로 접속한다. UI는 SDK를 직접 호출하지 않는다.

호스트 생성 옵션은 GDD의 고정 인원과 참가 코드 흐름을 그대로 따른다.

- `MaxPlayers = 6`이며 호스트를 포함한다.
- 쿼리·빠른 참가에 노출하지 않는 비공개 세션으로 만들고 참가 코드로만 입장한다.
- `WithRelayNetwork()`를 사용해 Relay 할당과 NGO Host 연결을 함께 시작한다.
- Relay 지역은 고정하지 않고 SDK의 지연 시간 기반 자동 선택을 사용한다.
- MVP는 호스트 이전을 활성화하지 않는다.

클라이언트는 입력한 참가 코드의 앞뒤 공백을 제거하고 대문자로 정규화한 뒤
`JoinSessionByCodeAsync()`를 호출한다. 생성·참가가 완료되기 전 중복 요청은 같은 진행 작업을
공유하고, 성공한 뒤에는 세션 ID·참가 코드·호스트 여부만 로컬 읽기 모델로 노출한다.
토큰이나 Relay 접속 정보는 UI와 로그에 출력하지 않는다.

세션을 생성하거나 참가하기 전에 `NetworkManager`와 `UnityTransport`가 존재해야 한다. MPS가
Relay 정보 설정과 Host/Client 시작을 담당하며, 세션 종료는 `NetworkManager.Shutdown()`을 직접
호출하지 않는다. 일반 참가자는 `ISession.LeaveAsync()`를 사용하고 호스트는 호스트 이전 없이
`IHostSession.DeleteAsync()`로 세션을 종료한다. 실패하면 메인 메뉴에 머물고 코드 만료,
세션 참가 불가, 인증, Relay 연결 문제를 사용자가 해결할 수 있는 문장으로 표시한다.

### 4.3 로비 참가자 동기화

MPS Session과 NGO 연결이 끝나면 씬의 `LobbyRosterNetwork`가 서버 권위 로비 상태를 구성한다.
클라이언트는 색상과 준비 변경만 RPC로 요청하고, `LobbyRosterService`가 현재 참가 여부,
색상 중복과 시작 조건을 재검증한다.

- 공개 참가 상태는 `NetworkList<LobbyPlayerNetworkState>`로 전원에게 복제한다.
- 참가 순서대로 0~5 슬롯을 배정하고, 비어 있는 6색 중 첫 색상을 초기 색상으로 배정한다.
- 색상은 파랑·노랑·초록·빨강·보라·주황이며 같은 색상은 동시에 선택할 수 없다.
- 참가자 공개 상태는 Client ID, 슬롯, 표시 이름, 색상, 준비, 호스트 여부만 포함한다.
- 정상 시작은 호스트 요청, 정확히 6명, 전원 준비를 모두 만족해야 한다.
- 연결 종료 시 해당 슬롯과 색상을 반환하고 `NetworkList`에서 제거한다.
- 거부 결과는 요청한 클라이언트에만 전달하고 UI는 서버 판정을 다시 계산하지 않는다.

호스트 시작 요청이 승인되면 서버는 로비 슬롯과 색상을 각 참가자의 `NetworkPlayerAvatar`에
기록하고 NGO 통합 씬 관리로 `10_Laboratory`를 `Single` 로드한다. `P_Player_Network`는
연결 시 서버가 생성하는 Player NetworkObject이며 씬 전환 뒤에도 유지된다. 각 소유자는
자기 슬롯의 시작점으로 이동하고 owner-authoritative `NetworkTransform`으로 2D 위치만
전송한다. 단일 방향 캐릭터 본체는 회전 동기화하지 않고 로컬 조준 피벗만 손전등 방향을
표현한다. 원격 인스턴스의 입력·이동 컴포넌트는 비활성화하고 로컬 소유자만 카메라와 입력을
연결한다. 개발 빌드에서는 현재 인원으로 시작할 수 있지만 릴리스의 정상 시작 조건은 우회하지
않는다.

같은 시작 요청에서 서버는 `RoleAssignmentService`로 참가자 한 명만 빌런으로 정하고,
`NetworkPlayerAvatar`의 소유자 읽기 전용 역할 값에 기록한다. 역할 표시 UI는 로컬 소유
플레이어에서만 7초 동안 열리며 다른 클라이언트에는 해당 역할 값이 직렬화되지 않는다.

동일 PC 검증은 `_Project/Settings/PlayMode/HostClient2.asset`을 사용한다.
Unity의 `Default` Play Mode로 `10_Laboratory`를 직접 실행하면 역할 배정·회의가 없는
1인 로컬 생존자 연습 모드로 동작한다. 역할 공개와 전체 네트워크 라운드는
`HostClient2`에서 `00_Bootstrap` 로비를 거쳐 시작해야 한다. 로컬 연습에서도 이동·Tab 지도와
30초 시작 보호 이후 원숭이 감지는 확인할 수 있다.
`Tools > Monkey Lab > Configure Host Client Play Mode`가 Main Editor와 Player 2의 초기 씬을
`00_Bootstrap`으로 맞추고 해당 시나리오를 활성화한다. 시나리오 실행 후
`Test Host Client Relay`는 실제 Relay 참가 코드로 2개 인스턴스를 연결하고 양쪽 로스터가
`2/6`인지 확인한 뒤 테스트 세션을 정리한다.

---

## 5. 주요 런타임 서비스

| 서비스 | 책임 |
| --- | --- |
| `GameSessionService` | 세션 생성·참가·종료·재접속 |
| `LobbyRosterService` | 참가자, 색상, 준비 상태 |
| `RoundStateMachine` / `NetworkRoundState` | 전체 라운드 상태·서버 시계·공개 상태 복제 |
| `RoleAssignmentService` | 정확히 한 명의 빌런 배정 |
| `PlayerStateService` | 생명·행동 상태 |
| `NetworkInteractionRules` | 소유권·순서·거리·점유·경로 검증 |
| `NetworkFuseStationAuthority` | 퓨즈 점유와 승인·해제 RPC |
| `NetworkPlayerMissionJournal` | 소유자 전용 개인 미션 목록·완료 상태와 전원 공개 수행 중 여부 |
| `MissionService` | 배정, 입력, 성공·실패 |
| `ProjectProgressService` | 포인트와 단계 보상 |
| `NoiseService` | 소음 생성·후보 조회 |
| `MonsterDirector` | 괴물 생성, 강화, 공통 관리 |
| `MonsterBrain` | 개별 AI 상태 머신 |
| `NetworkMonsterAuthority` | 서버 AI 실행과 괴물 상태·위치 복제 |
| `InfectionService` | 감염, 타이머, 사망 |
| `NetworkInfectionAuthority` | 공개 생명 상태·소유자 감염 타이머 복제 |
| `AntidoteService` | 레시피, 제작기, 아이템 |
| `VillainAbilityService` | 스피커와 강화 |
| `ClueService` | 단서 생성·조사 |
| `MeetingService` | 회의 상태와 채팅 |
| `VoteService` | 투표와 퇴출 |
| `RoundWinConditionService` | 우선순위 승패 판정 |
| `GameEventLogger` | 플레이 테스트 사건 기록 |

각 서비스는 단일 책임을 유지한다. `NetworkRoundState`가 모든 세부 로직을 직접 구현하지 않고
`RoundStateMachine`, `ProjectProgressService`, `RoundWinConditionService`의 호출 순서와 네트워크
복제만 조정한다.

---

## 6. 네트워크 모델

### 6.1 토폴로지

```text
Client 1 ─┐
Client 2 ─┤
Client 3 ─┼─ Unity Relay ─ Host/Server Player
Client 4 ─┤
Client 5 ─┘
```

Relay는 주소 노출과 NAT 문제를 줄이지만 전용 서버가 아니다. 호스트 종료 시 MVP 경기는 종료된다.

### 6.2 권한

| 대상 | 권한 |
| --- | --- |
| 라운드 상태·타이머 | 서버 |
| 역할·미션 배정 | 서버 |
| 플레이어 입력 | 소유 클라이언트 |
| 플레이어 최종 위치 | MVP는 소유자 전송 + 서버 속도 검증 |
| 상호작용 결과 | 서버 |
| 괴물 AI·위치 | 서버 |
| 미션 성공·진행률 | 서버 |
| 감염·아이템 | 서버 |
| 회의·투표·승패 | 서버 |
| 카메라·로컬 UI | 클라이언트 |

MVP 이동은 반응성을 위해 owner-authoritative NetworkTransform을 허용하되 서버가 최대 속도, 순간이동, 상호작용 거리를 검증한다. 이 방식은 완전한 안티치트가 아니며 정식 경쟁 서비스에서는 서버 권한 이동과 예측을 재검토한다.

### 6.3 NetworkObject 후보

- Player
- Monster
- MissionStation
- AntidoteMachine
- AntidotePickup 또는 보관 슬롯
- Door
- Speaker 상태 표시
- Clue
- RoundNetworkState

정적 벽과 장식 프롭은 NetworkObject로 만들지 않는다.

일반 방 프롭은 정적 `EnvironmentPropSlot`이며 네트워크 상태를 갖지 않는다. 자동문만
씬 `NetworkObject`로 두고 `NetworkAutomaticDoorAuthority`가 서버 물리 센서에서 플레이어와
괴물을 집계해 `IsOpen`을 복제한다. `AutomaticDoorMotor`는 복제된 상태에 따라 양쪽 패널과
통행 콜라이더를 함께 전환한다. 네트워크가 시작되지 않은 로컬 회색상자에서는 같은 센서
규칙을 로컬로 수행한다.

`P_Player_Network`의 초기 구성은 `NetworkObject`, owner-authoritative `NetworkTransform`,
`NetworkPlayerAvatar`, 이동·입력 컴포넌트와 색상·개인 역할 프레젠터다. 로컬 M1
프로토타입은 온라인 씬 진입 시 비활성화한다. 퓨즈 스테이션은 씬 `NetworkObject`와
`NetworkFuseStationAuthority`를 사용하고, 서버가 송신자 소유권·증가한 요청 순서·1.5m
거리·직선 경로·독점 점유를 승인한 뒤에만 소유 클라이언트의 미션 화면을 연다. 이동·연결
종료·10초 무입력에는 점유를 해제한다.

### 6.4 동기화 값과 사건

지속 상태는 NetworkVariable/NetworkList, 순간 연출은 RPC 또는 메시지 사건을 사용한다.

#### 지속 상태

- 라운드 상태와 남은 탐색 시간
- 공개 프로젝트 포인트
- 프로젝트 단계
- 플레이어 공개 생명 상태
- 괴물 상태·위치·목표 표현
- 제작기 상태와 완료 시각
- 강화 단계
- 회의 상태와 남은 시간
- 활성 단서

#### 순간 사건

- 미션 성공·실패 연출
- 소음 발생 표시
- 물기 애니메이션
- 감염 시작 개인 알림
- 스피커 재생
- 단계 복구 배너
- 투표 결과
- 승패 연출

`GameplayFeelView`는 위 순간 사건과 로컬 카메라 대상만 구독해 현재 방 배너, 조준점,
상호작용 표식, 미션 결과와 추격 가장자리 경고를 합성한다. 온라인 추격 표시는
`NetworkMonsterAuthority.IsThreateningClient`만 사용하고 괴물 좌표를 UI로 전달하지 않는다.
`TopDownCamera`의 이동 선행과 위치 흔들림, `PlayerMotionFeel`의 보행 바운스는 표현 계층에서만
계산하며 이동·물림·미션 판정에는 영향을 주지 않는다. 보행 바운스는 이동 거리 1m당 2.8rad의
느린 주기로 계산하고 상하 진폭은 0.012m, 스쿼시는 0.6%, 기울기는 1.2도로 제한한다.

연구소 월드는 직교 카메라를 유지하는 2.5D 혼합 시점으로 표시한다.
`MixedPerspectiveSceneStyler`는 기존 씬의 아래쪽 벽 정면을 아래로 확장하고,
`EnvironmentPropSlot`은 바닥 footprint를 충돌 기준으로 유지한 채 가구 몸체와 그림자만
발 위치 기준으로 세운다. 플레이어·괴물·가구의 sorting order는 `YSortedRenderer`의 같은
발 Y 공식으로 계산한다. 이 표현 보정은 물리 좌표, 마우스 조준, 네트워크 위치에 관여하지 않는다.

월드 스프라이트는 `M_WorldSpriteLit`의 URP 2D Lit 셰이더와 전용 `Renderer2DData`를 사용한다.
에디터 부팅 시 파이프라인이 Forward Renderer를 가리키면 `ProjectBootstrap`이 자동으로
2D Renderer로 교체한다. 0% 전역 비상광은 강도 0으로 고정하고 각 플레이어의 실루엣 개인등은
강도 0.006·외곽 반경 0.5m, 손전등은 외곽 반경 8m·54도 원뿔로 제한한다. 네트워크
플레이어의 개인등·손전등 렌더러는 해당 소유자
클라이언트에서만 켜서 다른 플레이어의 시야 광원이 내 화면을 밝히지 않게 한다. 0% 유도 표식과
상태등만 `M_IndicatorUnlit`으로 약하게 자체 발광한다. `ProjectMilestoneWorldPresenter`는
25% 유도등(반경 1.35m), 50% 보안실(반경 3m), 75% 탈출 경로(반경 2.5m), 100% 최종 출구
순서로 국소 `Light2D` 강도를 올린다. 조명은 로컬 표현이며 진행률 판정은 서버 값을 쓴다.

방향 손전등은 `FlashlightController`가 54도 안에 49개의 2D 광선을 쏘아 벽·닫힌 문·정적
장애물의 첫 충돌 지점까지만 런타임 원뿔 메시를 만든다. 플레이어·괴물과 Trigger는 차폐 대상에서
제외하며, 문 충돌체가 비활성화된 열린 출입구로는 빛이 통과한다. 따라서 직교 카메라와 물리 좌표를
바꾸지 않고도 벽 뒤 방 바닥에 손전등이 투영되지 않는다.

### 6.5 비밀 정보

역할, 개인 미션, 레시피, 빌런 쿨타임은 모든 클라이언트가 읽는 NetworkVariable에 넣지
않는다. 서버 저장 후 대상 클라이언트 RPC 또는 소유자 전용 메시지로 전송한다.
M2 역할 값은 `NetworkVariableReadPermission.Owner`,
`NetworkVariableWritePermission.Server`를 사용한다.

호스트 플레이어는 프로세스 메모리상 서버 정보를 볼 수 있으므로 MVP는 악의적인 호스트에 대한 보안을 보장하지 않는다.

---

## 7. 네트워크 요청 규격

모든 변경 요청에는 다음 공통 필드를 둔다.

```text
senderClientId
playerNetworkId
clientSequence
requestedServerStateVersion
payload
```

서버는 다음을 검사한다.

- 송신자가 해당 플레이어의 소유자인가?
- 요청 순서가 이전보다 새로운가?
- 현재 라운드 상태에서 허용되는가?
- 거리와 역할 조건이 맞는가?
- 이미 처리된 대상이 아닌가?

실패 응답은 개발 로그에 이유를 남기고 사용자에게 필요한 경우만 짧은 메시지를 표시한다.

---

## 8. 데이터 설계

### 8.1 ScriptableObject

| 에셋 | 내용 |
| --- | --- |
| `SO_GameBalance` | 타이머, 속도, 범위, 쿨타임 |
| `SO_MissionCatalog` | 미션 종류, 난이도, 포인트 규칙 |
| `SO_MonsterConfig` | 감지, 이동, 물기, 수색 |
| `SO_UpgradeConfig` | 단계별 강화 값 |
| `SO_MapConfig` | 방, 순찰 지점, 스폰 후보 |
| `SO_AudioCatalog` | 사건별 클립과 믹서 그룹 |
| `SO_UITheme` | 색, 아이콘, 공통 스타일 |

런타임에 ScriptableObject 값을 직접 변경하지 않는다. 필요하면 라운드 설정 복사본을 만든다.

### 8.2 런타임 모델

- `RoundStateModel`
- `PlayerRuntimeState`
- `MissionRuntimeState`
- `MonsterRuntimeState`
- `InfectionRuntimeState`
- `AntidoteMachineState`
- `MeetingRuntimeState`
- `VoteRuntimeState`
- `ClueRuntimeState`

네트워크 전송용 DTO와 도메인 모델을 구분한다. Unity Object 참조를 로그나 네트워크 DTO에 직접 넣지 않는다.

---

## 9. 플레이어 구현

### 9.1 컴포넌트 구성

```text
P_Player
├── NetworkObject
├── NetworkTransform
├── Rigidbody2D
├── CapsuleCollider2D
├── PlayerInputReader
├── PlayerMotor
├── PlayerInteractor
├── PlayerNetworkBridge
├── PlayerVisuals
├── PlayerAudio
└── PlayerStatePresenter
```

입력, 이동, 네트워크, 표현을 한 스크립트에 합치지 않는다.

### 9.2 이동 검증

- 서버는 짧은 시간창의 최대 이동 거리를 검사한다.
- 허용값을 넘으면 마지막 유효 위치로 보정한다.
- 회의·미션·사망 중 이동을 거부한다.
- 충돌 우회로 벽을 통과한 경우 서버 상호작용 검증이 반드시 실패한다.
- `GhostMovementController`는 유령 콜라이더를 트리거로 바꾸되 연구소 바닥 외곽에서 2m만
  확장한 `LaboratoryMapBounds` 안으로 `PlayerMotor`의 다음 위치와 물리 틱 뒤 위치를 모두 제한한다.
  로컬 플레이어와 네트워크 프리팹은 같은 경계 상수를 사용한다.

---

## 10. 괴물 구현

괴물 AI는 호스트에서만 실행한다.

```text
P_Monster
├── NetworkObject
├── NetworkTransform
├── Rigidbody2D
├── CapsuleCollider2D
├── MonsterBrain
├── MonsterSenses
├── MonsterBiteController
├── MonsterAnimatorPresenter
└── MonsterAudio
```

- 클라이언트는 2D 경로 그래프 결정을 하지 않는다.
- AI 틱은 매 프레임이 아니라 5~10Hz를 시작값으로 한다.
- 위치는 네트워크 보간한다.
- 서버의 활성 `MonsterBrain`은 각자 가진 순찰 지점을 하나의 후보 집합처럼 공유한다.
  최근 방문 기억을 제외하고 다른 개체의 현재·예약 위치에서 가장 먼 후보를 선택하며,
  `MonsterPatrolReservation`은 정확한 좌표뿐 아니라 설정된 최소 간격 안의 중복 예약도 막는다.
- `FixedUpdate` 이동은 기존 경로 방향에 제한된 분리 조향을 더해 충돌을 무시하는 원숭이끼리도
  포개지지 않게 한다. 같은 소음 현장에서는 선두가 도착한 뒤 후속 개체가 분리 반경 안의
  현재 위치를 도착점으로 인정하되 급습 감지 원점은 실제 소음 좌표를 유지한다.
- 이동 감시자는 2초 무진행 시 같은 목적지 경로를 최대 3회 다시 만들고, 계속 실패하면
  현재 상태의 기존 실패 전이로 넘긴다.
- 평상시 감지는 방향이 없는 원형 반경과 2D 경로 접근 가능성만으로 대상을 선택한다. 손전등 상태와 이동 여부는 조건이 아니다(GDD 1.6).
- 미션 실패 위치 도착 시 반경 5.33m, 빌런 스피커 위치 도착 시 반경 8m 안의 접근 가능한
  대상을 선택해 조사 속도로 추적한다. 두 강제 급습은 도착 시점부터 10초간 반경을 유지하고,
  표적이 없거나 놓치면 소음원 반경 안의 그래프 노드를 반복 선택해 추격 속도로 배회한다.
- 추격 경로는 2D 웨이포인트 그래프로 계산하며 물기 직전에는 물리 장애물을 다시 검사한다.
- 소음 후보는 공간 인덱스 또는 방 기준 목록으로 좁힌다.
- 일반 `InvestigateNoise` 이동 중에는 `MonsterTierRuntime`의 현재 근접 감지 반경만 사용한다.
  미션 실패와 스피커는 강제 급습으로 분류해 각각
  `MonsterBalanceConfig.MissionFailureAmbushRadius`와 `SpeakerAmbushRadius`를 사용하고,
  기존 `Chase`·미해결 `Bite`도 취소하며 현장 도착 전에는 다른 근접 표적으로 전환하지 않는다.
  10초 강제 배회가 끝나면 급습 반경을 폐기하고 현재 강화 단계의 평상 반경으로 복귀한다.
- `NetworkPlayerAvatar`는 소유자의 손전등 입력을 서버 Rpc로 검증·복제하지만, 이 값은
  연출용이며 감지에 쓰지 않는다. `MonsterTarget.CanBeDetectedBy`는 `IsDetectable`만
  확인하므로 `Proximity`와 `NoiseAmbush`가 같은 조건을 쓴다(GDD 1.6). 감염·유령으로
  `IsDetectable`이 꺼진 대상만 감지에서 빠진다.
- 물기 성공 결과는 `MonsterBrain`이 보관하고 감염된 표적을 감지 대상에서 제외한다.
  물기 회복 시 강제 소음 10초가 남아 있으면 `Search` 기반 현장 배회로, 아니면 순찰로 복귀한다.
- 감염이 시작되면 `InfectionService`가 `MonsterTarget`을 감지 불가로 바꾸고, 치료 성공 시에만
  다시 감지 가능으로 복구한다.

---

## 11. 미션 프레임워크

```text
IMissionDefinition
  Id
  Category
  Difficulty
  NoiseOnFailure
  CreateInstance(seed)

IMissionInstance
  ValidateInput(input)
  CurrentState
  IsComplete
  IsFailed
```

각 미니게임은 공통 수명주기와 서버 입력 검증을 사용한다. UI는 미션 유형별 Presenter로 분리한다.

M3 회색상자는 `FuseStationPrototype`의 공통 수명주기를 재사용하되
`MissionPrototypeKind`로 퓨즈·차단기·CCTV·시료 분류·배터리 운반·압력 밸브·보안 회로·안테나 조율·서버 로그 복구를 구분한다. 서버는 스테이션
`NetworkObjectId`를 개인 미션 ID로 사용한다. 어려운 차단기가 포함된 분산 후보는 4개,
쉬운 미션 중심 후보는 5개를 배정하고 한 사람에게 최소 세 종류가 섞이게 한다.
클라이언트와 서버는 승인 RPC의 같은 시드로 퍼즐을 만들며, 클라이언트는 성공 bool 대신
퓨즈 드롭·차단기 타이밍 클릭·CCTV 노드/포트 2단계 선택·시료 선택/분류·배터리 분리/장착/낙하·밸브 개방도/잠금·회로 모듈 회전/전원 인가·안테나 축 이동/신호 고정·로그 키 입력
입력을 보낸다. 서버의
`MissionValidationSession`이 순서와 짝, 동기화된 서버 시간의 차단기 허용 구간을 검증한
뒤에만 완료와 프로젝트 점수를 확정한다.

비상 배터리는 `BatteryTransportMissionInstance`의 `Secured → Carrying → Completed` 단계와
별도 `BatteryReceiverPrototype`을 사용한다. 운반 중에는 시작 스테이션 거리 제한 대신
플레이어 이동을 점유 활동으로 인정하고, 장착 입력 시 서버가 수신 단말기와의 거리·경로를
검증한다. 낙하 위치에서 Medium 소음을 발생시키며 운반 속도는 이동 설정의
`BatteryCarryMoveSpeed`를 사용한다.

압력 밸브는 `PressureValveMissionInstance`가 두 개방도의 평균 압력과 안전 구간 진입 시각을
소유한다. 클라이언트는 0~1 개방도를 0~1000 정수로 양자화해 보내고, 서버는 자체 압력과
안정화 시간을 다시 계산해 잠금 입력을 판정한다. 원형 드래그 중 입력은 0.1초 간격으로
제한해 연속 조작감은 유지하면서 RPC 과다 전송을 막는다.

보안 회로는 `SecurityCircuitMissionInstance`가 시드 기반 목표 회전과 각 모듈의 현재 회전을
소유한다. 안테나는 `AntennaAlignmentMissionInstance`가 방위·주파수 축의 한 칸 이동을,
서버 로그는 `ServerLogRecoveryMissionInstance`가 시드 기반 키 순서와 진행 인덱스를 검증한다.
세 미션 모두 클라이언트가 완료 여부나 최종 좌표를 보내지 않고 개별 조작만 전송한다.

빌런 강화는 `VillainUpgradeMissionSession`이 축별로 기존 검증 규칙을 조합한다. 후각은
`PressureValveMissionInstance`, 개체는 `SecurityCircuitMissionInstance`, 독성은
`BreakerTimingMissionInstance`를 사용한다. `NetworkUpgradeStationAuthority`가 승인 시드와
서버 시작 시각을 소유자에게만 보내며, 이후 개별 입력을 서버 세션에 재현해 완료된 경우에만
`NetworkVillainUpgradeAuthority.ServerTryApplyUpgrade`를 호출한다. 다른 클라이언트에는
점유·수행 연출만 보이고 퍼즐 내용과 강화 단계는 전송하지 않는다.

`NetworkMonsterAuthority`는 서버가 이미 추적·물기 상태로 확정한 대상의 clientId만 함께
복제한다. 미션 UI는 그 값이 로컬 플레이어와 같을 때 화면 가장자리 경고를 표시하며,
괴물의 정확한 방향·거리·벽 너머 위치는 계산하거나 노출하지 않는다.

`NetworkPlayerMissionJournal`은 개인 미션 ID와 완료 목록을 소유자에게만 복제한다.
`MissionJournalView`는 이 목록을 좌측 상시 HUD와 `Tab` 화면의 좌측 목록에 표시한다.
지도 본체는 `Resources/UI/T_LaboratoryMap` 정적 텍스처를 사용하며, 보정된 방 중심과 삼각형
보간으로 로컬 플레이어의 월드 좌표만 이미지 UV로 변환한다. 미확인 구역, 다른 플레이어와
개인 미션 표식은 지도 위에 그리지 않는다. 다른 클라이언트의 개인 목록은 읽거나 그리지 않는다.
다른 플레이어에게는 미션 ID 없이 수행 중 여부만 공개해 캐릭터 수행 동작을 표시한다.
스테이션은 점유 중 밝기 변화를 전원에게 보여 주고, 완료 순간에는 일시적인 완료 연출 RPC를
전송한다. 개인별 완료 색상은 완료한 소유자의 화면에만 유지하며, 빌런을 포함한 다른
플레이어는 전체 프로젝트 진행도로 누적 결과를 확인한다.

```text
MissionStationNetwork
→ MissionService
→ MissionInstance
→ MissionResult
→ ProgressService / NoiseService
```

---

## 12. 회의와 채팅

- 채팅 메시지는 서버를 경유한다.
- 현재 회의 참가자인지 검증한다.
- 메시지 길이와 전송 빈도를 제한한다.
- HTML/Rich Text 태그를 제거하거나 이스케이프한다.
- 결과 이후 저장이 필요하지 않으므로 MVP는 영구 보관하지 않는다.
- 유령 채팅은 별도 채널 ID를 사용한다.

`NetworkGhostChatAuthority`는 송신자가 `DeadGhost`인지 서버에서 다시
확인한다. 메시지는 `NetworkList`에 저장하지 않고 현재 유령인 클라이언트에게만
대상 지정 Rpc로 중계한다. 따라서 생존자 클라이언트 메모리에도 유령 채팅
원문이 남지 않는다.

투표는 서버의 `Dictionary<PlayerId, VoteTarget>`에 마지막 유효값만 저장한다.

---

## 13. 오디오·VFX·애니메이션 연결

게임 규칙이 연출 완료에 의존하지 않게 한다.

- 서버 사건: 물기 시작 시각과 판정 시각
- 클라이언트: 해당 타임라인에 애니메이션 재생
- 애니메이션 이벤트: 발소리·보조 VFX만 사용
- 승패·감염 결과: 서버 사건을 기준으로 표시

오디오 클립 이름을 게임 코드에서 문자열로 찾지 않고 카탈로그 에셋으로 매핑한다.

`PresentationAssetCatalog`은 조명·탈출구 표시, 미션 성공·실패,
헬기·RX-9 가스 프리팩과 오디오 클립의 교체 지점이다. 에셋이 비어 있으면
신호 검증이 가능한 프로토타입 스프라이트를 사용하고, 최종 에셋은 카탈로그만
교체한다. `ProjectMilestoneWorldPresenter`는 25/50/75/100% 단계의 월드
`Light2D`·표지판·탈출 마커를, `RoundEndingSequencePresenter`는 헬기 도착과
제한 시간 초과 가스 방출을 라운드 복제 결과에서 재생한다.

---

## 14. 저장과 로그

### 14.1 로컬 저장

- 그래픽 설정
- 오디오 설정
- 키 설정
- 닉네임
- 접근성 설정

JSON 또는 PlayerPrefs 래퍼를 사용한다. 민감 정보와 인증 토큰을 직접 저장하지 않는다.

`LocalGameSettings`가 PlayerPrefs 키를 한곳에서 관리하고 값 변경 사건을 로컬
Presentation에 전달한다. `RuntimeSettingsOverlay`는 씬보다 먼저 생성되어 메뉴와
게임 씬에서 같은 `F1` 설정 화면을 제공한다. 화면 흔들림·점멸·비네팅은 기존 연출의
최종 강도에 곱하며 서버 판정과 월드 조명 범위에는 영향을 주지 않는다.

`RuntimeAudioVolumeRouter`는 씬 로드 시 한 번만 AudioSource를 수집해 음악·효과·위험
카테고리 음량을 적용한다. 새로 제작하는 프리팹은 `SettingsAudioSource`에 카테고리와
기본 음량을 명시해 이름이나 매 프레임 검색에 의존하지 않는다.

### 14.2 플레이 테스트 로그

- 호스트에서 JSON Lines 또는 CSV로 저장
- 라운드 ID와 서버 상대 시각 포함
- 닉네임 대신 라운드 내 익명 플레이어 ID 사용 가능
- 릴리스 빌드에서는 사용 동의와 보존 정책 없이 외부 전송하지 않음

---

## 15. 성능 예산

| 항목 | 목표 |
| --- | --- |
| 프레임 | 평균 60fps, 최저 30fps 이상 |
| 프레임 CPU | 16.6ms 목표 |
| 플레이어 | 6 |
| 괴물 | 최대 8 |
| 동적 그림자 조명 | 화면 기준 제한, 품질 프리셋 제공 |
| CCTV | 활성 화면 수 1, 필요 시 저해상도 RenderTexture |
| GC Alloc | 탐색 정상 프레임에서 최소화 |
| AI 판단 | 서버 5~10Hz 시작값 |
| 네트워크 상태 | 필요한 값만 저빈도 또는 변화 기반 동기화 |

### 15.1 주요 최적화

- 정적 환경 Static Batching 또는 SRP Batcher 활용
- 반복 프롭 공유 머티리얼
- 객체 풀링: VFX, 임시 표시, 필요 시 괴물
- CCTV는 보고 있을 때만 렌더
- UI 목록 재사용
- `Physics2D` 라인캐스트와 2D 경로 그래프 계산 분산
- 개발 빌드에서 Unity Profiler로 호스트와 클라이언트 각각 측정

CCTV는 보안실 월드 단말기의 E 상호작으로 연다. 서버는 50% 해제,
생존 상태, 거리, 단일 사용자 점유를 검증한다. 클라이언트는 7개 직교 카메라 중
선택한 하나만 640×360 런타임 `RenderTexture`로 렌더하며, 닫기·거리 이탈·회의
진입·연결 종료 시 점유와 카메라 렌더를 풀어준다.

재접속은 Unity 인증 PlayerId를 연결 승인 payload로 보내 새 NGO clientId를 기존 신원에
연결한다. 서버는 연결 종료 직전 역할·슬롯·위치·개인 미션·감염·해독제 스냅샷을 보관하고
30초 안에 돌아오면 새 PlayerObject와 프로젝트 완료 이력을 함께 복원한다. 유예가 만료된
생존자의 미완료 미션은 `NetworkRoundState`의 공용 복구 목록으로 이동한다. 클라이언트에는
스테이션 ID만 공개하고 원래 소유자와 점수 예산은 서버에만 유지해, 다른 살아 있는 생존자가
완료해도 이탈자의 남은 개인 예산으로 프로젝트 점수를 계산한다. 유령은 자기 개인 미션만
계속할 수 있고 공용 복구 미션은 조작할 수 없다.

`RoundHudView`의 F10 개발 패널은 서버 인스턴스이며 에디터 또는 Development 빌드에서만
활성화한다. 역할, 생명 상태, 해독제, 프로젝트 단계, 남은 시간, 강화 3축과 강제 승패는
`NetworkRoundState`가 서버 전용 개발 API로 다시 검증한 뒤 기존 NetworkVariable/Rpc 경로로
복제한다. 개체 단계는 `NetworkMonsterPopulationSpawner`가 4/6/8마리 활성 상태를 즉시 맞춘다.
패널은 `NetworkMonsterAuthority`의 복제 상태·목표 client ID를 읽고, 마지막 `NoiseEventData`의
경로 반경을 월드 디버그 원으로 표시한다. 릴리스 빌드에서는 패널과 원을 생성하지 않는다.

---

## 16. 테스트 전략

### Edit Mode

- 프로젝트 포인트 계산
- 투표 동률·기권
- 승패 우선순위
- 감염 단계 고정
- 소음 우선순위
- 미션 중복 완료 방지

### Play Mode

- 플레이어 이동·상호작용
- 괴물 상태 전환
- 제작기 수명주기
- 회의 진입·복귀
- 유령 권한

### 멀티플레이

- Host + Client 1
- Host + Client 5
- 100ms 지연과 패킷 손실
- 동시 아이템 획득
- 동시 회의 호출
- 마지막 미션과 사망 동시 판정
- 클라이언트 재접속

Unity의 공식 casual co-op quickstart는 Netcode for GameObjects, Multiplayer Services와 Multiplayer Play Mode를 이용한 로컬 다중 플레이어 테스트 흐름을 제공한다.

- [Casual co-op quickstart](https://docs.unity.com/en-us/multiplayer/quickstarts/casual-co-op-quickstart)

---

## 17. 빌드 구성

| 구성 | 용도 |
| --- | --- |
| `Development` | 디버그 패널, 상세 로그, 프로파일러 연결 |
| `Demo` | 심사 시연, 개발 치트 숨김 또는 암호화된 패널 |
| `Release` | 사용자 배포, 디버그 기능 제거 |

빌드 정보에 Git 커밋, 문서 버전, Unity 버전과 빌드 시각을 포함한다.

### 17.1 실패 대비

- Relay 연결 실패 메시지와 재시도
- 세션 코드 만료 안내
- 서비스 장애 시 로컬 샌드박스 모드
- 해커톤 심사용 녹화 영상
- 가능하면 동일 PC 다중 클라이언트 데모

---

## 18. 구현 승인 기준

- 패키지 버전이 `Packages/manifest.json`과 Lock 파일에 고정돼 있다.
- 역할과 개인 데이터가 공용 NetworkVariable에 없다.
- 게임 규칙은 호스트에서 판정한다.
- 연결 지연으로 미션·아이템·투표가 중복 처리되지 않는다.
- 씬 전환 후 이전 라운드 NetworkObject와 이벤트 구독이 남지 않는다.
- 6명, 괴물 8마리 환경에서 성능 목표를 만족한다.
- 연속 3판 후 메모리와 이벤트 호출이 누적되지 않는다.
