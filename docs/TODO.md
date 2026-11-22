# TODO / 알려진 한계

## 다음 세션 시작점 (2026-11-20 기준)

대화 기록은 PC 간에 넘어가지 않으므로, 이어서 작업할 때는 이 문단과 [UI_DESIGN.md](UI_DESIGN.md)부터 읽는다.

- **완료:** 설계 확정(UI_DESIGN, SERVER_DESIGN). 0단계(`GameNumber`, `GameSession`, `GameInitializer`) 구현 — 배치 모드에서 컴파일, EditMode 테스트(22개 중 20개 통과, 실패 2개는 아래 "테스트" 항목의 기존 실패), 플레이 모드(저장 → 업그레이드 → 재시작 후 유지) 확인.
- **사용자 확인 대기:** 에디터에서 직접 플레이해 업그레이드 패널 비용 표시와 골드 HUD가 정상으로 보이는지(배치 모드는 화면을 못 봄).
- **다음 결정:** 실행 중인 에디터를 조작하는 Unity CLI + Pipeline 패키지(`com.unity.pipeline`)를 설치할지, 패키지를 커밋할지(CLAUDE.md "외부 도구" 참고).
- **1차 진행 중 (2026-11-22, 집 PC):** 반응형 타입(`Assets/Scripts/Reactive/`, UI_DESIGN 5-5) 구현 — 격리 사본의 배치 모드에서 컴파일과 규약 동작 확인. 아직 어떤 모델/UI도 쓰지 않는다. 파생 상태는 쓰는 곳이 생길 때 추가.
- **옛 브랜치 반영 결정 완료 (2026-11-22):** 집 PC `UIManager_improvement`는 병합하지 않는다. `.gitignore` 규칙, `BalanceConfig`, 코드리뷰의 미해결 항목만 옮겼다. UIRegistry와 Canvas 정리는 UI_DESIGN이 대체하고, SingletonBehaviour는 보류(아래 "코드 품질"), 세이브 로드 실패 처리는 아래 "저장"에서 따로 고친다.
- **다음 작업:** 1차 수직 슬라이스 — `SkillManagePopup` 하나를 WindowRegistry → WindowManager → Presenter → 반응형 구독/자동 해제 → InputBlocker까지 새 구조로 통과시켜 인터페이스를 확정(UI_DESIGN 9장).

---

UI 구조 정리 과정에서 의도적으로 범위 밖으로 둔 항목들. 해결하지 않았다는 사실을 숨기지 않기 위해 기록한다.

## UI

- **`BossChallengeButton` ↔ `GameManager` 양방향 참조 정리**
  버튼이 스스로 `Hide()`하고 `GameManager.EnterBossBattle()`을 직접 호출한다. UI와 게임 루프가 서로를 알고 있는 관계.
  UI 구조 정리(패널 종류/스택)와 성격이 달라서 분리함. 이벤트 기반으로 바꾸는 방향 검토.

- **탭 잠금 규칙이 `TabBarController`에만 있음**
  잠금 상태(`isUnlocked`)를 탭 바가 들고 있어서, 코드에서 `UIManager.ShowUI<탭패널>()`을 직접 부르면 잠금을 우회한다.
  탭이 늘어나면 잠금 규칙을 어느 쪽이 소유할지 정해야 함.

- **탭 위에 전체 화면을 올리는 구조 미지원**
  UI 스택은 `Overlay`(팝업)와 `Exclusive`(탭) 두 모드만 지원. 탭 → 전체 화면 → 뒤로가기로 탭 복귀하는 흐름은 불가.
  상세 화면을 팝업으로 만들면 현재 규칙으로 가능하고, 정말 필요해지면 `StackMode`에 세 번째 값을 추가하면 됨.

- **선택된 탭 강조 표시 없음**
  `TabBarController`가 현재 탭을 모른다. `UIManager`의 탭 변경 이벤트는 UI 구조 정리 때 추가하지만, 강조 표시 UI 자체는 별도 작업.

- **스택이 비었을 때 뒤로가기는 아무 동작도 안 함**
  종료 확인창은 아직 없음.

- **Canvas 순서가 암묵적이고 화면 페이드가 UI를 덮지 못함**
  Canvas 3개(`UIManager`, `StaticCanvas`, `DynamicCanvas`)가 전부 order 0이라 하이어라키 순서에 의존한다. `FadePanel`이 HUD와 탭바보다 아래라 전환 중에도 UI가 보이고 눌린다.
  UI_DESIGN 4-1의 정렬 구분(페이드는 System)으로 1차에서 해결한다.

## 렌더링

- **월드 오브젝트의 Sorting Layer 정리**
  프로젝트의 Sorting Layer가 `Default` 하나뿐이라, 배경/캐릭터/몬스터/이펙트/월드 HP바(`HpBar.prefab`, World Space Canvas)의
  앞뒤 순서를 전부 Order in Layer 숫자 하나로 맞추고 있다. `Background → Character → WorldFx → WorldUI` 같은 Sorting Layer로 나누면
  "HP바는 항상 캐릭터 위" 같은 규칙이 숫자 맞추기가 아니라 구조로 보장된다.
  화면 UI는 Screen Space - Overlay라 Sorting Layer가 적용되지 않으므로 이 작업과 무관하다(화면 UI 순서는 코드 enum → sortingOrder 구간으로 관리).
  현재 순서가 깨지는 증상은 없어서 범위 밖으로 둠.

## 저장

- **손상된 세이브를 처리하지 못함** (2026-11-22 격리된 플레이 모드에서 재현)
  - JSON이 잘린 파일: `Load()` 예외로 `GameInitializer.Start()`가 중단되어 안내 없이 게임이 시작되지 않는다(파일은 덮어쓰지 않음).
  - JSON 형식은 맞지만 `stats`가 없는 파일(`{}`, `{"gold":5}`): 에러 없이 레벨 1로 로드되고 종료 시 그대로 덮어써져 **진행도가 유실된다.**
  - `UIManager_improvement`의 `SaveStorageBase`는 첫 번째 경우만 막는다. `JsonUtility`가 없는 `stats`를 기본 객체로 채우므로 `data.stats == null` 검사로는 두 번째 경우를 잡지 못한다.

- **세이브 버전과 마이그레이션 없음**
  저장 형식에 버전이 없어서 필드 이름이나 구조를 바꾸면 기존 진행도를 옮길 방법이 없다. 버전 필드와 버전별 변환 경로가 필요하다.
  오프라인 보상의 마지막 저장 시각처럼 필드를 추가할 때 함께 설계한다. 필수 키 검사를 도입한 뒤라면 새 필드가 없는 옛 세이브를 손상으로 판정하지 않아야 한다.

- **저장이 원자적이지 않고 백업이 없음**
  `LocalFileSaveStorage.Save`가 원본에 바로 덮어써서, 쓰는 도중 앱이 죽으면 파일이 잘린다.
  임시 파일에 쓴 뒤 교체하고 직전 정상본을 `.bak`으로 남긴다. 플랫폼별(WebGL, Android) 교체 동작은 확인이 필요하다.

- **손상된 세이브의 복구 수단 없음**
  손상을 감지해 멈추기만 하면 사용자는 게임을 계속할 방법이 없다. 백업으로 복구하거나, 명시적으로 확인받은 뒤 새로 시작하는 선택지가 필요하다.

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
