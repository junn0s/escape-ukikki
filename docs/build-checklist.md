# 빌드 체크리스트

> 문서 버전: 1.0
> 목적: CI가 없는 동안 사람이 손으로 확인할 최소 절차 (production-roadmap.md M0 "수동 빌드 체크리스트")

---

## 1. 환경 전제

| 항목 | 값 |
| --- | --- |
| Unity | 6000.3.20f1 |
| 프로젝트 경로 | `game/` |
| 렌더 파이프라인 | URP (`Assets/_Project/Settings/URP_Laboratory.asset`) |
| 타깃 | Windows x64 (Windows Build Support 모듈 필요) |

팀 전원이 같은 Unity 버전을 쓴다. 버전이 다르면 `ProjectSettings/ProjectVersion.txt`가 바뀌어
불필요한 충돌이 생긴다.

---

## 2. 커밋 전 확인

- [ ] `game/Library`, `Temp`, `Logs`, `UserSettings`가 스테이징에 없다
- [ ] 새로 만든 에셋의 `.meta`가 함께 스테이징됐다
- [ ] 씬·프리팹·머티리얼이 LFS가 아니라 일반 Git으로 들어간다 (`git lfs status`로 확인)
- [ ] FBX·PNG·WAV 등 바이너리는 LFS로 들어간다
- [ ] `Packages/packages-lock.json`이 함께 커밋됐다

---

## 3. EditMode 테스트

```bash
"/Applications/Unity/Hub/Editor/6000.3.20f1/Unity.app/Contents/MacOS/Unity" \
  -projectPath game -batchmode -runTests -testPlatform EditMode \
  -testResults /tmp/results.xml -nographics -logFile /tmp/unity-test.log
```

- [ ] 종료 코드 0
- [ ] `/tmp/results.xml`의 `failed="0"`
- [ ] 로그에 `error CS` 없음

밸런스 수치를 바꿨다면 `MonkeyLab.Tests.EditMode`의 `GameBalanceDefaultsTests`가 먼저 깨져야
정상이다. 테스트를 고치기 전에 `docs/balance-and-telemetry.md`를 먼저 고쳤는지 확인한다.

---

## 4. 에디터 실행 확인

- [ ] `00_Bootstrap` 씬이 에러 없이 열린다
- [ ] Console에 Missing Script / Missing Prefab 경고가 없다
- [ ] Build Settings 씬 목록의 0번이 `00_Bootstrap`이다
- [ ] 90/91 샌드박스 씬이 비활성(체크 해제) 상태다

---

## 5. Windows 빌드

> Windows Build Support 모듈 설치 후에 수행한다. 미설치 상태에서는 이 절을 건너뛰고
> 그 사실을 `docs/devlog.md`에 남긴다.

```bash
# Unity Hub → Installs → 6000.3.20f1 → Add modules → Windows Build Support (Mono)
```

- [ ] Development Build로 빌드가 성공한다
- [ ] 실행 파일이 실행되고 `00_Bootstrap`이 로드된다
- [ ] 씬 전환이 동작한다
- [ ] 빌드 로그에 셰이더 컴파일 에러가 없다

---

## 6. 빌드 정보

기술 설계서 §17에 따라 빌드에 다음을 포함한다. M0에서는 수동 기록으로 대신하고,
자동 주입은 별도 작업으로 남긴다.

- Git 커밋 해시
- 문서 세트 버전
- Unity 버전
- 빌드 시각
