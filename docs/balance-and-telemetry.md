# 밸런스·텔레메트리 설계

> 문서 버전: 1.4
> 성격: 첫 플레이 테스트를 위한 초기값  
> 주의: 수치는 확정 재미가 아니라 검증 가능한 출발점이다.

---

## 1. 밸런스 목표

- 양 진영 승률이 장기적으로 40~60% 범위에 들어간다.
- 첫 감염은 보통 시작 2분 이후 발생한다.
- 첫 강화 흔적은 대부분의 판에서 4분 이전에 생긴다.
- 생존자는 미션만 해도, 추리만 해도 이길 수 없고 둘을 병행해야 한다.
- 빌런은 강화할수록 강해지지만 의심도 함께 커진다.
- 최종 괴물 8마리 상태가 위험하지만 피할 수 없는 확정 사망은 아니다.
- 해독제는 물린 뒤 제작하는 수단이 아니라 사전 준비 자원이다.

---

## 2. 라운드와 회의

| 키 | 초기값 |
| --- | ---: |
| 탐색 시간 | 900초 |
| 시작 보호 | 30초 |
| 역할 공개 | 5초 |
| 첫 회의 잠금 | 탐색 120초 |
| 회의 공용 쿨타임 | 탐색 120초 |
| 최대 회의 | 3회 |
| 토론 | 90초 |
| 투표 | 30초 |
| 결과 표시 | 5초 |
| 회의 종료 물기 보호 | 2초 |
| 채팅 최대 글자 수 | 80자 |
| 채팅 전송 최소 간격 | 1초 |
| 채팅 보관 메시지 | 60개 |
| 연결 종료 재접속 유예 | 30초 |

`SO_RoundBalance_Default`는 채팅 값을 `ChatMessageMaximumLength`,
`ChatMessageIntervalSeconds`, `ChatHistoryMaximumCount` 필드에,
재접속 유예를 `DisconnectGraceSeconds` 필드에 연결한다(GDD 19.2).
토론 채팅은 회의 토론 단계에서만 열리며 탐색 중 일반 채팅은 MVP 범위가 아니다(GDD 16.2).

회의를 세 번 모두 사용하면 실제 세션 길이는 로딩과 결과를 포함해 약 20분 이상이 될 수 있다. 심사 데모에서는 개발 설정으로 시간을 단축할 수 있지만 정상 밸런스 프로필은 유지한다.

---

## 3. 플레이어 이동

| 키 | 초기값 |
| --- | ---: |
| 기본 이동 속도 | 4.0m/s |
| 배터리 운반 속도 | 3.0m/s |
| 유령 이동 속도 | 4.8m/s |
| 회전 속도 | 720°/s |
| 일반 상호작용 거리 | 1.5m |
| 아이템 획득 거리 | 1.2m |
| 독점 점유 무입력 해제 | 10초 |

달리기·스태미나는 MVP에서 제외한다. 도입하면 소리와 추격 전체를 다시 조정해야 한다.

`SO_InteractionBalance_Default`는 `generalInteractionRangeMeters`를
`GeneralInteractionRangeMeters`, `exclusiveOccupancyTimeoutSeconds`를
`ExclusiveOccupancyTimeoutSeconds` 필드에 연결한다.

---

## 4. 괴물

| 키 | 초기값 |
| --- | ---: |
| 순찰 속도 | 2.6m/s |
| 일반 추격 속도 | 4.6m/s |
| 소리 조사 속도 | 6.0m/s |
| 소리 가속 최대 시간 | 6초 |
| 방 체류 | 6초 ± 1초 |
| 수색 시간 | 5초 |
| 소음 현장 급습 감지 반경 | 8m |
| 물기 거리 | 콜라이더 표면 간 0.9m |
| 물기 준비 | 0.35초 |
| 물기 후 재행동 | 1.2초 |
| 피격자 물기 보호 | 1.5초 |
| AI 판단 빈도 | 8Hz 시작값 |
| 발걸음 판정 최소 속도 | 0.15m/s |
| 발걸음 정지 유예 | 0.2초 |

소리 조사 속도 6.0m/s는 플레이어 4.0m/s의 1.5배다.

`SO_MonsterBalance_Default`는 `footstepMinimumSpeedMetersPerSecond`를
`FootstepMinimumSpeedMetersPerSecond`, `footstepReleaseDelaySeconds`를
`FootstepReleaseDelaySeconds` 필드에 연결한다. 정지 유예는 8Hz AI 판단 사이에서 짧은
네트워크 위치 갱신이 빠지는 것을 막기 위한 값이다.

### 4.1 강화

| 축 | 기본 | 1회 | 2회 |
| --- | ---: | ---: | ---: |
| 불빛·발걸음 근접 감지 반경 | 5.0m | 7.0m | 9.0m |
| 괴물 수 | 4 | 6 | 8 |
| 새 감염 제한시간 | 90초 | 60초 | 30초 |

### 4.2 공정성 보호

- 추가 괴물은 3초 예고 후 활성화
- 문 하나 주변의 목표 괴물이 과도하게 겹치면 회피 우선순위 분산
- 같은 생존자가 연속으로 물리는 것을 1.5초 보호
- 물기 성공 후 감염자를 감지 대상에서 제외하고 회복 뒤 즉시 순찰 복귀
- 평상시와 소리 조사 이동 중에는 손전등이 켜졌거나 걷는 표적만 현재 강화 단계의 원형 근접 감지 반경에서 즉시 추격
- 근접 추적 중 소등하고 정지하면 다음 AI 판단 틱에 표적을 잃지만, 소음 현장 급습 표적은 그대로 유지
- 소음 위치 도착 시 반경 8m 안의 접근 가능한 표적을 조사 속도로 급습
- 감염 상태에서는 추격·물기를 하지 않고, 해독 후 다시 감지

---

## 5. 소음

| 강도 | 경로 반경 | 예시 |
| --- | ---: | --- |
| Small | 12m | 작은 장비 실수 |
| Medium | 30m | 퓨즈·시료 실패, 배터리 낙하 |
| Large | 40m | 스피커, 격리실 경보 |

- 퓨즈 실패의 초기 Medium 반경은 전력 복구실과 바로 연결된 인접 방·복도 정도를 기준으로 한다.
- 같은 소음의 경로 반경 안에 있는 괴물은 한 마리만 선택하지 않고 모두 반응한다.

### 5.1 소음 지속

- 사건 자체는 순간 발생한다.
- 괴물의 기억 목표는 도착 또는 최대 6초 가속 이후 수색까지 유지한다.
- 스피커는 3초간 반복 재생하지만 하나의 소음 사건 ID를 사용한다.

### 5.2 조정 기준

- 소음을 내도 아무 괴물도 반응하지 않는 사건이 50%를 넘으면 반경을 늘린다.
- 소음 실패가 곧바로 감염으로 이어지는 비율이 40%를 넘으면 반경이나 괴물 속도를 낮춘다.
- 플레이어가 소리 원인을 이해하지 못하면 수치보다 연출과 UI를 먼저 고친다.

---

## 6. 스피커와 강화

| 키 | 초기값 |
| --- | ---: |
| 스피커 쿨타임 | 45초 |
| 스피커 소음 반경 | 40m 경로 거리 |
| 스피커 재생 | 3초 |
| 강화 미션 목표 조작 시간 | 12~18초 |
| 강화 중단 | 즉시 초기화 |
| 축별 최대 완료 | 2회 |
| 후각 혼합 목표 | 시드별 45~75% |
| 후각 혼합 허용 오차 | ±8% |
| 후각 안정화 유지 | 1.5초 |
| 후각 네트워크 판정 유예 | 0.12초 |
| 개체 회로 모듈 | 3개 |
| 독성 주입 단계 | 3회 |
| 독성 게이지 왕복 주기 | 2초 |
| 독성 성공 허용 오차 | ±12% |
| 독성 네트워크 판정 유예 | 0.12초 |

`SO_UpgradeBalance_Default`는 위 조작값을 `ChallengeItemCount`,
`ScentTargetMinimumNormalized`, `ScentTargetMaximumNormalized`,
`ScentToleranceNormalized`, `ScentStabilizeSeconds`, `ToxicityCycleSeconds`,
`ToxicitySuccessToleranceNormalized`에 연결한다. 네트워크 판정은
`ScentNetworkToleranceSeconds`, `ToxicityNetworkToleranceSeconds`만큼 서버 허용 구간을
보정한다.

스피커의 목표는 확정 처치가 아니라 동선 방해와 알리바이 조성이다. 스피커 사용 후 15초 안에 물린 사건이 지나치게 많으면 쿨타임보다 반경과 맵 배치를 먼저 조정한다.

---

## 7. 미션과 진행률

### 7.1 포인트

내부 프로젝트 총점은 10,000이다.

| 개인 미션 수 | 개인 총점 | 미션당 기본점 |
| ---: | ---: | ---: |
| 4개 | 2,000 | 500 |
| 5개 | 2,000 | 400 |

모든 생존자가 전체의 20%를 담당한다.

### 7.2 수행 시간 목표

| 미션 | 정상 수행 | 실패 가능성 | 실패 소음 |
| --- | ---: | --- | --- |
| 퓨즈 | 8~15초 | 중간 | Medium |
| 차단기 | 8~12초 | 높음 | Medium |
| CCTV 재부팅 | 12~20초 | 낮음 | Small 또는 없음 |
| 시료 분류 | 10~18초 | 중간 | Medium |
| 배터리 운반 | 이동 포함 20~40초 | 낙하 | Medium |
| 압력 조정 | 12~20초 | 중간 | Medium |

`SO_FuseMission_Default`는 공통 항목 수 3개, 차단기 왕복 주기 2초,
안전 구간 허용 오차 0.12, 네트워크 판정 여유 0.12초, 시료 보관함 3개를 초기값으로 사용한다.
퓨즈는 드래그 순서 입력, 차단기는 클릭/Space 타이밍 입력, CCTV는 신호 노드와 포트의
2단계 선택, 시료는 분석 대상과 보관함의 2단계 선택으로 서로 다른 조작을 제공한다.

압력 밸브의 초기 플레이테스트 값은 목표 압력 0.65, 안전 구간 ±0.08,
안정화 유지 2초, 네트워크 판정 여유 0.12초다. 두 밸브 개방도의 평균을 압력계 입력으로
사용하고, 안전 구간을 유지한 뒤 잠금 레버를 당겨야 완료된다. 비상 배터리 운반 중에는
기존 이동 밸런스 표의 `batteryCarryMoveSpeed` 3.0m/s를 적용한다.

한 생존자의 순수 미션 조작 시간은 약 60~100초, 이동과 위험 회피를 포함한 실제 완료 시간은 5~9분을 목표로 한다.

### 7.3 진행률 목표 시점

| 단계 | 목표 중앙값 |
| ---: | --- |
| 25% | 탐색 3~4분 |
| 50% | 탐색 6~8분 |
| 75% | 탐색 10~12분 |
| 100% | 탐색 12~15분 |

---

## 8. 해독제

| 키 | 초기값 |
| --- | ---: |
| 제작기 | 2대 |
| 제작 시간 | 180초 |
| 제작기 동시 큐 | 각 1개 |
| 소지 한도 | 1개 |
| 사용 시간 | 1.5초 |
| 사용 중 이동 | 취소 |
| 완성품 수명 | 라운드 종료까지 |
| 보관 칸 슬롯 | 칸당 2개 |

`SO_AntidoteBalance_Default`는 위 값을 다음 필드에 연결한다.

| 밸런스 키 | SO 필드 |
| --- | --- |
| 제작 시간 | `CraftDurationSeconds` |
| 제작기 | `FabricatorCount` |
| 제작기 동시 큐 | `FabricatorQueueCapacity` |
| 소지 한도 | `MaxCarryCount` |
| 사용 시간 | `UseDurationSeconds` |
| 보관 칸 슬롯 | `StorageLockerSlotCount` |

### 8.1 목표 경제

- 첫 물림 전에 최소 한 개의 해독제가 완성되는 판이 절반 이상이어야 한다.
- 30초 독성 단계에서 생존 가능성은 사전 제작과 위치 판단에 의존한다.
- 완성품 선점은 의심을 만들지만 한 사람이 여러 개를 독점할 수 없게 소지 한도를 유지한다.

### 8.2 조정 순서

생존자가 너무 자주 치료하지 못할 때:

1. 레시피 후보 위치를 더 읽기 쉽게 함
2. 제작기 접근 동선을 개선
3. 제작 시간 180→150초 검토
4. 감염 시간 조정

독성 최종 단계 30초는 핵심 강화 보상이므로 가장 마지막에 변경한다.

---

## 9. 맵 이동 목표

| 구간 | 목표 |
| --- | --- |
| 인접 방 | 5~10초 |
| 루프 반대편 | 15~25초 |
| 맵 대각선 | 25~35초 |
| 백신실 A→B | 30~40초 |

8마리 상태에서 평균 우회 시간이 지나치게 길어지면 속도보다 문 폭, 분기와 괴물 분산을 먼저 수정한다.

### 9.1 자동문

| Balance key / SO field | 초기값 |
| --- | ---: |
| `openSpeedMetersPerSecond` / `OpenSpeedMetersPerSecond` | 8m/s |
| `closeDelaySeconds` / `CloseDelaySeconds` | 0.75초 |
| `sensorDepthMeters` / `SensorDepthMeters` | 4m |
| `panelSlideDistanceMeters` / `PanelSlideDistanceMeters` | 2.15m |

자동문은 플레이어와 괴물을 같은 조건으로 감지한다. 닫힘 지연은 뒤따르는 플레이어가 문에
끼이지 않게 하는 값이며, 지나치게 길면 추격 중 문 위치 정보가 과도하게 노출되는지 확인한다.

---

## 10. 목표 지표

### 10.1 결과

- 생존자 승률: 40~60%
- 빌런 승률: 40~60%
- 프로젝트 승리와 퇴출 승리가 모두 발생
- 평균 경기 탐색 시간: 10~15분

### 10.2 참여

- 생존자 개인 미션 완료율 중앙값: 70% 이상
- 회의 1회 이상 발생: 80% 이상
- 빌런 스피커 사용: 판당 3회 이상 중앙값
- 빌런 강화: 판당 2개 축 이상 사용
- 한 판에서 현장 단서 한 개 이상 발견: 80% 이상

### 10.3 공정성

- 시작 2분 이내 사망: 10% 미만
- 물린 뒤 입력 불가·끼임으로 사망: 0%
- 괴물 길막으로 우회 불가능한 사건: 0%
- 원인을 이해하지 못한 감염 응답: 20% 미만

---

## 11. 텔레메트리 공통 필드

모든 사건에 다음을 기록한다.

```text
schemaVersion
eventName
roundId
serverTimeSeconds
roundState
mapId
buildVersion
```

플레이어 관련 사건:

```text
playerRoundId
role
lifeState
roomId
```

실제 계정 ID와 채팅 원문은 플레이 테스트 로그에 저장하지 않는다.

---

## 12. 핵심 사건 필드

### `mission_completed`

```text
missionId
missionType
ownerPlayerRoundId
stationId
durationSeconds
attemptCount
progressBefore
progressAfter
```

### `noise_emitted`

```text
noiseId
sourceType
roomId
radius
respondingMonsterCount
```

### `infection_started`

```text
targetPlayerRoundId
monsterId
toxicityLevel
durationSeconds
roomId
```

### `upgrade_completed`

```text
upgradeType
newLevel
stationId
roomId
elapsedRoundTime
```

### `meeting_resolved`

```text
meetingIndex
callerPlayerRoundId
voteCounts
exiledPlayerRoundIdOrNull
discussionMessagesCount
```

### `round_ended`

```text
winner
reason
explorationElapsed
projectProgress
aliveSurvivorCount
upgradeLevels
```

---

## 13. 분석 순서

1. 기술 오류와 중도 종료를 정상 밸런스 데이터에서 제외한다.
2. 진영 승률과 종료 이유를 확인한다.
3. 시간대별 프로젝트 진행과 강화 단계를 겹쳐 본다.
4. 감염 원인을 평상시 근접 감지와 소음 현장 급습으로 분리한다.
5. 단서 생성과 발견, 회의 결과를 연결한다.
6. 수치 변경은 한 번에 한 축만 적용한다.
7. 최소 5판, 가능하면 10판 이상 비교한 뒤 결론을 낸다.

---

## 14. 밸런스 변경 기록 양식

```text
날짜:
빌드:
변경값:
변경 이유:
기대 효과:
비교할 지표:
테스트 판수:
결과:
유지/복구/추가 변경:
```

느낌만으로 여러 값을 동시에 바꾸지 않는다.
