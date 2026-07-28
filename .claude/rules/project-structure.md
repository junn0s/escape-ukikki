---
paths:
  - "game/Assets/**/*"
---

# 폴더·네이밍 규칙 (docs/project-structure.md §2, §5, §6 기준)

## 프로젝트 소유 파일
프로젝트 소유 파일은 `_Project` 안에 둔다. 외부 패키지를 임의로 수정해 `_Project` 안으로 복사하지 않는다.

## 에셋 네이밍
| 종류 | 규칙 | 예시 |
| --- | --- | --- |
| Prefab | `P_` | `P_Player`, `P_Room_Security` |
| ScriptableObject | `SO_` | `SO_GameBalance_Default` |
| Material | `M_` | `M_LabMetal_Blue` |
| Texture | `T_` | `T_LabMetal_BaseColor` |
| Static Mesh | `SM_` | `SM_LabDoor` |
| Skinned Mesh | `SK_` | `SK_Player` |
| Animation | `A_` | `A_Player_Interact` |
| Animator | `AC_` | `AC_Monkey` |
| Audio | `SFX_`, `AMB_`, `MUS_` | `SFX_Speaker_On_01` |
| VFX | `VFX_` | `VFX_VentSmoke_Red` |
| UI | `UI_` | `UI_Icon_Antidote` |

파일명에는 공백, 한글, 괄호, `final`, 버전 번호를 넣지 않는다.

## 프리팹 구성
- 프리팹 루트 이름과 파일명을 일치시킨다.
- 씬에 프리팹을 풀어서 수정하지 않고 필요한 경우 Prefab Variant를 사용한다.

## 씬 명명
| 파일 | 역할 |
| --- | --- |
| `00_Bootstrap.unity` | 영구 서비스 |
| `01_MainMenu.unity` | 메인 메뉴 |
| `02_Lobby.unity` | 로비 |
| `10_Laboratory.unity` | 라운드 맵 |
| `90_ArtSandbox.unity` | 아트 테스트 |
| `91_GameplaySandbox.unity` | 시스템 테스트 |
