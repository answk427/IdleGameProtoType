# TODO / 알려진 한계

## 다음 세션 시작점 (2026-11-23 기준)

대화 기록은 PC 간에 넘어가지 않으므로, 이어서 작업할 때는 이 장 전체와 [UI_DESIGN.md](UI_DESIGN.md)부터 읽는다.
새 세션에는 이렇게 시작하면 된다:

> docs/TODO.md의 "다음 세션 시작점"과 docs/UI_DESIGN.md를 읽고, PC 준비 상태를 확인한 뒤 "다음 작업" 순서대로 하나씩 제안해 줘.

### 1. PC 준비

- **브랜치는 `refactor/ui-stack`.** 기본 브랜치 `main`에는 이 문서와 새 구조가 없다.
- **Git LFS 필수**(폰트, 텍스처 등 255개). 없이 받았으면 설치 후 `git lfs install`, `git lfs pull`.
- **Unity 6000.4.9f1.** WebGL 작업을 하려면 WebGL Build Support 모듈도 필요하다. `Library` 없이 받아도 그대로 열린다(새 clone에서 컴파일, 테스트, 씬 부팅 확인).
- **프로젝트 폴더 경로는 짧게.** 120자 정도의 깊은 경로에서는 패키지 일부가 Windows 경로 길이 제한에 걸린다.
- **Claude 메모리:** 비공개 저장소 claude-sync의 README "IdleGameProtoType 설정"대로 PC마다 한 번 설정한다. 없어도 아래 "작업 방식"으로 이어갈 수 있다.
- ⚠️ **엑셀 재동기화 금지.** 현재 `.asset`이 사실상 원본이다(아래 "데이터").
- 세이브 파일은 PC별이라 넘어오지 않는다.

### 2. 작업 방식 (사용자와 합의)

- 한 항목씩 **제안 → 사용자 확인 → 실행.** 커밋과 push는 명시적으로 지시받을 때만. "정리해"는 분류해서 보고만 하고 스테이징도 하지 않는다.
- **UI는 UI_DESIGN이 기준.** 바꾸자고 하기 전에 11장을 읽고, 원안에 작은 구현 규칙을 더해서 되는지 먼저 시험한다.
- 구현 중 합의한 설계를 구조적으로 바꿔야 하면 멈추고 먼저 묻는다.
- 프로젝트 성격은 **포트폴리오 유지**, 단 세이브 유실 금지는 원칙.
- **검증은 격리 사본에서.** 프로젝트를 다른 폴더에 복사하고 그 사본의 `ProjectSettings.asset`에서 `companyName`/`productName`을 바꾼 뒤 배치 모드로 돌린다(실제 세이브 경로와 분리). 검사용 스크립트는 저장소에 넣지 않는다.
- 배치 모드는 화면을 못 본다. 화면과 실제 입력은 사본을 WebGL로 빌드해 브라우저에서 확인한다(숨은 탭은 3fps 정도로 느려지므로 더블탭, 전투 결과를 믿지 말 것).

### 3. 완료된 것

- **설계 확정:** UI_DESIGN, SERVER_DESIGN.
- **0단계:** `GameNumber`, `GameSession`, `GameInitializer`.
- **1차 수직 슬라이스:** 스킬 관리 창 하나가 `WindowRegistry` → `WindowManager` → `SkillManagePresenter` → 반응형 구독/자동 해제 → `InputBlocker`를 통과한다(`Assets/Scripts/UI/Core/`). 기존 `UIManager`(HUD, 탭)와의 연결은 `UIRoot` 한 곳. 새 팝업은 `UIWindow`로 만든다(절차는 CLAUDE.md "UI 구조").
- **손상 세이브 유실 수정:** `SaveDataValidator`. 남은 것은 아래 "저장".
- **옛 브랜치 `UIManager_improvement`는 병합하지 않기로 결정:** `.gitignore` 규칙, `BalanceConfig`, 코드리뷰/백로그의 유효 항목만 옮겼다. UIRegistry와 Canvas 정리는 UI_DESIGN이 대체, SingletonBehaviour는 보류(아래 "코드 품질"). 집 PC에만 남은 그 브랜치의 커밋 4개와 stash는 **이어가는 데 필요 없다.**

### 4. 다음 작업 (추천 순서)

| | 작업 | 시작 전에 정할 것 | 끝났다고 볼 기준 |
|---|---|---|---|
| 1 | WebGL 세이브 소실 (아래 "저장") | 프로젝트 템플릿 추가 승인 | 새로 빌드한 것을 새로고침해도 진행도 유지 |
| 2 | 전투 영구 정지 (아래 "전투", 재현법 있음) | 수정 방식 | 재현법의 시드 1~3이 1500초 동안 멈추지 않음 |
| 3 | 스킬 쿨다운 초기화 (아래 "전투") | 쿨다운을 무엇 기준으로 보관할지 | 슬롯을 옮겨도 남은 쿨다운 유지 |
| 4 | 세이브 오류 창 | [보완안](UI_REDESIGN_PROPOSAL.md)의 B8(스택 삽입 순서), B9(로드 실패 시 UIRoot 준비) 승인 | 닫히지 않고 탭바를 막으며, 종료해도 세이브 바이트 동일 |
| 5 | 탭 2개 이관 | - | `WindowOptions`에 Exclusive 추가, `UIRoot`의 탭 연결 제거, 기존과 동작 동일 |
| 6 | HUD, 보스 버튼, StageAnnouncement 이관 | - | 모델(레벨, 경험치, 골드, 보스 가능)과 이벤트 버스가 반응형, `UIManager` 삭제 |

6번에서 같이 할 것: `Subject`에 정적 버스 초기화 수단 추가(UI_DESIGN 5-3), 파생 상태 타입 추가(쓰는 곳이 생길 때), `PlayerStats`의 이관용 C# 이벤트 제거.
그 뒤: 전체 화면/캐시 정책 옵션(쓰는 창이 생길 때), 드로콜과 리빌드 실측(UI_DESIGN 4-4), 스피너 아트(지금은 흰 사각형), Android 실기기 확인(뒤로가기, 더블탭, 세이브).

### 5. 사용자 결정/확인 대기

- **원본 엑셀이 어디 있는지.** 집 PC의 프로젝트, 바탕화면, 문서, 다운로드에서는 파일명으로 찾지 못했다. 다른 PC에 있는지 확인.
- **슬라임(1001) 처치 보상 골드 10000, 경험치 100**이 테스트용으로 남은 값인지(JSON은 111, 0).
- **BossUndead(2002) HP 1000 → 10000**을 반영할지. `origin/UIManager_improvement`의 `3c15d5e`에 섞여 있어 일부러 가져오지 않았다.
- **웹에서 체크포인트 사이 진행 유실**을 어떻게 막을지. "주기적 자동저장은 두지 않는다"는 기존 결정(CLAUDE.md)과 부딪힐 수 있다.
- **Unity CLI + Pipeline 패키지**(`com.unity.pipeline`)를 설치할지, 커밋할지(CLAUDE.md "외부 도구").
- **실패 중인 EditMode 테스트 2개**를 고칠지 지울지(아래 "테스트").
- **에디터에서 직접 플레이한 눈 확인.** 업그레이드 비용, 골드 HUD, 스킬 창은 Claude가 WebGL 화면으로만 봤다.

---

UI 구조 정리 과정에서 의도적으로 범위 밖으로 둔 항목들. 해결하지 않았다는 사실을 숨기지 않기 위해 기록한다.

## UI

- **`BossChallengeButton` ↔ `GameManager` 양방향 참조 정리**
  버튼이 스스로 `Hide()`하고 `GameManager.EnterBossBattle()`을 직접 호출한다. UI와 게임 루프가 서로를 알고 있는 관계.
  UI 구조 정리(패널 종류/스택)와 성격이 달라서 분리함. 이벤트 기반으로 바꾸는 방향 검토.
  **버그(재현됨):** 보스 도전 가능 상태에서 죽으면 `BossAvailable`은 false가 되는데 버튼은 남고, `EnterBossBattle()`도 가능 여부를 검사하지 않는다. UI_DESIGN 5-3의 `BossAvailable` 상태 전환 때 해결.

- **탭 잠금 규칙이 `TabBarController`에만 있음**
  잠금 상태(`isUnlocked`)를 탭 바가 들고 있어서, 코드에서 `UIManager.ShowUI<탭패널>()`을 직접 부르면 잠금을 우회한다.
  탭이 늘어나면 잠금 규칙을 어느 쪽이 소유할지 정해야 함.

- **탭 위에 전체 화면을 올리는 구조 미지원**
  탭은 기존 `UIManager`(Exclusive), 팝업은 `WindowManager`(쌓임)만 지원. 탭 → 전체 화면 → 뒤로가기로 탭 복귀하는 흐름은 불가.
  상세 화면을 팝업으로 만들면 현재 규칙으로 가능하고, 정말 필요해지면 `WindowOptions`에 전체 화면 축을 추가하면 됨.

- **선택된 탭 강조 표시 없음**
  `TabBarController`가 현재 탭을 모른다. `UIManager`의 탭 변경 이벤트는 UI 구조 정리 때 추가하지만, 강조 표시 UI 자체는 별도 작업.

- **스택이 비었을 때 뒤로가기는 아무 동작도 안 함**
  종료 확인창은 아직 없음.

- **기존 UI 쪽 Canvas 순서가 암묵적**
  창, 딤, 페이드, 입력 차단은 정렬 구분으로 순서가 정해졌지만, 이관 전인 `UIManager`/`StaticCanvas`/`DynamicCanvas`는 전부 order 0이라 하이어라키 순서에 의존한다.
  그래서 데미지 숫자가 아직 HUD와 탭바 위에 그려진다(터치는 가로채지 않음). HUD 이관 때 Fx/Hud 구분으로 옮긴다.

## 렌더링

- **월드 오브젝트의 Sorting Layer 정리**
  프로젝트의 Sorting Layer가 `Default` 하나뿐이라, 배경/캐릭터/몬스터/이펙트/월드 HP바(`HpBar.prefab`, World Space Canvas)의
  앞뒤 순서를 전부 Order in Layer 숫자 하나로 맞추고 있다. `Background → Character → WorldFx → WorldUI` 같은 Sorting Layer로 나누면
  "HP바는 항상 캐릭터 위" 같은 규칙이 숫자 맞추기가 아니라 구조로 보장된다.
  화면 UI는 Screen Space - Overlay라 Sorting Layer가 적용되지 않으므로 이 작업과 무관하다(화면 UI 순서는 코드 enum → sortingOrder 구간으로 관리).
  현재 순서가 깨지는 증상은 없어서 범위 밖으로 둠.

## 전투

- **프레임이 튀면 게임 진행이 영구히 멈춤** (2026-11-22 재현, UI 슬라이스 이전 코드에서도 동일)
  몬스터가 `이동속도 × deltaTime`만큼 움직여서, 긴 프레임 한 번에 사거리를 건너뛰고 플레이어 뒤로 넘어간다. `GetClosestMonster`는 앞쪽만 보므로 그 몬스터는 타겟이 되지 않고,
  계속 왼쪽으로 달리며 살아 있어서 `AllDie()`가 영영 참이 되지 않는다(다음 스폰 없음).
  30fps에 100프레임당 한 번 0.333초 프레임을 섞으면 게임 시간 70~310초 안에 3번 중 3번 멈췄다. 몬스터 이동을 사거리 경계에서 멈추거나 지나친 몬스터도 타겟에 넣는 식의 수정이 필요하다(방식 미정).
  - **재현법(새 clone에서도 같은 값 확인):** 격리 사본에서 세이브를 레벨 1, 업그레이드 0, 경험치 0, 골드 0, 스테이지 1, `learnedSkillIds [1]`, 슬롯 `[-1,-1,1,-1,-1]`로 두고,
    씬 로드 전에 `Time.captureDeltaTime = 1f/30`. 씬 로드 후 시작한 코루틴에서 `r = new System.Random(시드)`, `yield return null` 한 번 뒤 매 프레임
    `Time.captureDeltaTime = r.Next(100) == 0 ? 1f/3 : 1f/30; yield return null;`을 반복한다. 게임 시간 40초 동안 골드가 안 늘면 정지로 본다(최대 1500초).
  - 현재 결과(마지막으로 골드가 는 시각): 시드 1 = 301.53초, 2 = 312.83초, 3 = 71.40초.

- **스킬을 다른 슬롯으로 옮기면 쿨다운이 0이 됨** (재현)
  `PlayerController.RefreshEquippedSkills`가 슬롯 단위로 비교해서, 자리가 바뀐 스킬을 `new Skill(entry)`로 새로 만든다. 옮기기만 하면 스킬을 계속 쓸 수 있다.
  후보: 슬롯이 아니라 스킬 id 기준으로 런타임 상태를 보관(방식 미정).

## 저장

- **WebGL 빌드에서 새로고침하면 세이브가 전부 사라짐** (2026-11-22 빌드로 재현)
  기본 템플릿의 `index.html`에 `config.autoSyncPersistentDataPath = true;`가 주석 처리돼 있어 `persistentDataPath`(IDBFS)가 브라우저 저장소로 내려가지 않는다.
  빌드 산출물에서 그 한 줄을 켜면 유지되는 것까지 확인했다. 빌드마다 손으로 고치지 않으려면 프로젝트 WebGL 템플릿이 필요하다.
  - 만드는 법(템플릿으로 빌드하는 것까지는 미검증): 에디터 설치 폴더의 `PlaybackEngines/WebGLSupport/BuildTools/WebGLTemplates/Base/Default`를
    `Assets/WebGLTemplates/<이름>`으로 복사 → `index.html`의 그 줄 주석 해제 → Player Settings의 WebGL Template을 그 템플릿으로(지금은 `APPLICATION:Default`).
  - 이미 만들어 배포한 WebGL 빌드에도 같은 문제가 있다. 다시 빌드해서 올려야 한다.
  별개로 웹은 새로고침/탭 닫기 때 `OnApplicationQuit`이 오지 않아 마지막 체크포인트 이후 진행(골드, 경험치)은 잃는다(31.5K → 21K로 재현).

- **손상된 세이브로 시작이 막혔을 때 안내가 없음** (유실 자체는 2026-11-22에 수정: `SaveDataValidator`가 변환 전에 막고 원본을 보존)
  - 콘솔 에러만 남고 게임이 시작되지 않는다. 안내창은 1차 슬라이스 뒤 System 구분의 창으로 만들고, `GameInitializer`의 로드 실패 지점에서 띄운다.
  - 막힌 상태에서도 탭바가 눌린다. 캐릭터 탭을 닫으면 `StatUpgradePanel.OnHidden`이 없는 `Wallet`에 접근해 예외가 난다(세이브 파일이 안전한 것은 에디터와 WebGL 빌드에서 확인).

- **골드가 10^15 이상이면 저장/로드 때 1씩 어긋날 수 있음**
  `JsonUtility`의 `double` 변환 오차다(10^15 미만은 어긋남 없음, 10^15대에서 최대 1). `GameNumber`의 정밀도 한계(약 9×10^15)보다 조금 일찍 나타난다.

- **세이브 버전과 마이그레이션 없음**
  저장 형식에 버전이 없어서 필드 이름이나 구조를 바꾸면 기존 진행도를 옮길 방법이 없다. 버전 필드와 버전별 변환 경로가 필요하다.
  오프라인 보상의 마지막 저장 시각처럼 필드를 추가할 때 함께 설계한다. 필수 키 검사를 도입한 뒤라면 새 필드가 없는 옛 세이브를 손상으로 판정하지 않아야 한다.

- **저장이 원자적이지 않고 백업이 없음**
  `LocalFileSaveStorage.Save`가 원본에 바로 덮어써서, 쓰는 도중 앱이 죽으면 파일이 잘린다.
  임시 파일에 쓴 뒤 교체하고 직전 정상본을 `.bak`으로 남긴다. 플랫폼별(WebGL, Android) 교체 동작은 확인이 필요하다.

- **손상된 세이브의 복구 수단 없음**
  손상을 감지해 멈추기만 하면 사용자는 게임을 계속할 방법이 없다. 백업으로 복구하거나, 명시적으로 확인받은 뒤 새로 시작하는 선택지가 필요하다.
  씬을 다시 로드하는 방식으로 만든다면 주의: 시작에 실패한 씬을 재로드하면 파괴된 `PlayerHud`가 `OnGameStarted` 구독에 남아 다음 시작을 예외로 끊는 현상이 간헐적으로 재현됐다(원인은 확정하지 못함. 지금 게임은 씬을 재로드하지 않음).

- **클라우드 세이브 / 기기 이전 없음**
  저장 구현체가 로컬 파일뿐이라 재설치나 기기 변경 때 진행도를 잃는다. SERVER_DESIGN의 `ServerSaveStorage`는 업로드와 이상치 검사까지만 정해져 있고, 새 기기로 내려받는 흐름은 없다.

## 데이터

- ⚠️ **`.asset`과 JSON이 어긋나 있어 재동기화하면 현재 데이터가 사라짐** (2026-11-23 대조)
  Syncer는 목록을 통째로 교체한다. 엑셀도 JSON과 같다면 아래처럼 되돌아간다. 원본 엑셀을 찾아 `.asset` 값으로 맞추기 전에는 재동기화하지 말 것.

  | 대상 | 현재 `.asset` | JSON |
  |---|---|---|
  | 몬스터 1001 | HP 40, 골드 10000, 경험치 100 | HP 30, 골드 111, 경험치 0 |
  | 몬스터 1002 | HP 60 | HP 50 |
  | 몬스터 2001 | HP 3000, 공격력 10, 사거리 0.1 | HP 100, 공격력 20, 사거리 1 |
  | 몬스터 2002 | 있음 | **없음** |
  | 스킬 6(저주) | 있음 | **없음** |
  | 스테이지 1 | 보스 2002, 간격 1.5 | 보스 2001, 간격 3 |
  | 스테이지 2 | 인카운터 1, 간격 1.5 | 인카운터 2, 간격 3 |
  | 스테이지 3 | 몬스터 4마리, 인카운터 1, 간격 1.5 | 1마리, 인카운터 2, 간격 3 |

## 성능 / 로딩

- **로딩 화면에서 데이터 인덱스 워밍업** (2026-08-02 사용자 요청)
  각 Database의 `Dictionary` 인덱스는 런타임에 만들어진다. 지금은 `GameDatabaseManager.Awake`(3개)와 첫 조회 시점(`PlayerStatDatabase`)에 만들어져서, 데이터가 수천 개가 되면 그 프레임이 튄다.
  로딩 씬이 생기면 `Warmup()` 같은 명시적 진입점을 열어 거기서 미리 만든다. 착수 시점은 부트 씬 분리 또는 데이터 규모가 커질 때(지금은 불필요).

## 문서

- **README가 1차 슬라이스 이전 구조를 설명함**
  UI 절은 모든 화면이 `UIBase` + 씬 배치 자동 등록이라고 설명하는데, 팝업은 이제 `UIWindow` + Registry + Presenter다. 저장 절에도 손상 검사와 차단이 없다. 포트폴리오 첫 화면이므로 1차 이관이 끝나기 전이라도 고칠 것.

## 빌드

- **필수 인스펙터 참조의 빌드 전 검사 없음**
  `GameInitializer`의 검사처럼 실행해야 드러나거나, 폴백에 가려진다(예: `CombatEffectManager`의 보상 드랍 참조 3개는 미연결 상태이고 임시 도형으로 표시된다).
  에디터 경고(`OnValidate`)와 빌드 실패 처리(`IPreprocessBuildWithReport`)를 검토한다. 의도된 폴백까지 막지 않도록 필수와 선택 참조를 먼저 구분한다.

## 코드 품질

2026-09 코드리뷰에서 미해결이던 항목 중 2026-11-22에 현재 코드에서 다시 확인한 것.

- **개발용 로그가 상시 출력됨**
  `PlayerHud.UpdateExpBar`는 경험치가 바뀔 때마다 2줄을 찍는다. `PlayerStats`(레벨업, 업그레이드, 학습), `GameManager`(보스전 시작)도 릴리스에 그대로 남는다.

- **`CombatEffectManager` 책임 과다**
  데미지 텍스트, 파티클, 보상 드랍, VFX, 임시 스프라이트 생성을 한 클래스가 한다. 임시 스프라이트는 지금 실제로 쓰이는 경로라, 아트를 연결하기 전에는 지우면 안 된다.

- **애니메이션 이름 처리 불일치**
  `PlayerController`는 직렬화 필드, `Monster`/`BossMonster`는 문자열(`"Idle"`, `"Death"`, `"Attack"`)을 코드에 박아 둔다.

- **null 처리와 이름 규칙 불일치**
  `Monster.PlayIdleAnimation`, `MonsterSpawner.AllDie`, `GameManager`의 `FadeController.Instance` 사용에 null 검사가 없다.
  `MonsterSpawner.SpawnMonster`는 컴포넌트가 없으면 `AddComponent`로 프리팹 오류를 가린다. `Monster.moveToPlayer`만 camelCase다.

- **싱글턴 규칙 불일치**
  중복 검사와 `DontDestroyOnLoad`가 매니저마다 다르고, 파괴될 때 `Instance`를 비우는 곳이 없다. 씬이 하나인 지금은 드러나지 않으므로 부트 씬을 나눌 때(UI_DESIGN 9장) 정한다.
  공통 베이스를 도입한다면 주의: `FadeController`는 `UIManager` 오브젝트에 붙어 있어 씬 간 유지로 만들면 UI 전체가 같이 유지된다.

- **이벤트 버스 규칙 불일치**
  `GlobalCombatEvents`만 `static class`가 아니다. 구독 시점도 `GameManager`는 Awake/OnDestroy, `CombatEffectManager`는 OnEnable/OnDisable로 다르다.

- **`PlayerStats`의 방어 코드 불일치**
  `saveData`는 곳곳에서 null을 검사하지만 `statData`와 `upgradeConfig`는 검사 없이 쓰는 곳이 있다.

- **쓰이지 않는 코드와 값**
  `JsonDataLoader`는 씬과 코드 어디에서도 쓰이지 않는다. `Monster`의 직렬화 필드 `maxHp`/`expReward`는 `Initialize`가 덮어써서 인스펙터 값이 의미 없다.

## 게임 기능

- **오프라인 보상 없음**
  방치형의 핵심 기능(앱을 끈 동안 쌓인 보상을 다음 접속 시 지급)이 아직 없다. 마지막 저장 시각을 세이브에 남기고,
  접속 시 경과 시간으로 보상을 계산하는 게임 쪽 기능이 필요하다. UI 쪽은 시작 팝업 순서(설계 문서 6-3, 창 닫힘 대기)로 받을 자리가 있다.
  경과 시간은 기기 시계가 아니라 서버 시간 기준으로 계산하고, 코드는 실제 시각을 `IClock`을 통해서만 얻는다
  ([SERVER_DESIGN.md](SERVER_DESIGN.md) 1장 규칙 3). 서버가 없는 지금은 `IClock`의 로컬 구현으로 시작한다.

## 테스트

- **`PlayerStatsTests`의 경험치 테스트 2개가 실패한 채로 남아 있음**
  `AddExp_ExactlyRequiredExp_LevelsUpAndCarriesRemainderZero`, `AddExp_LargeAmount_ChainLevelsUpAndKeepsRemainder`.
  레벨업이 자동에서 수동(`TryLevelUp()`, 캐릭터 탭의 "LEVEL UP!" 버튼)으로 바뀐 뒤 테스트가 갱신되지 않았다.
  0단계 작업 전부터 실패하던 것이며(0단계는 `AddExp`/`TryLevelUp`을 건드리지 않음), 테스트를 새 규칙에 맞게 고칠지 지울지는 따로 정한다.
