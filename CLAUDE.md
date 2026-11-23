# IdleGameProtoType

Unity 2D 방치형(idle) 액션 게임 프로토타입. Unity 6000.4.9f1.
**목표 플랫폼은 Android.** 플랫폼 동작과 최종 확인은 Android 빌드(+ adb)로 한다. WebGL은 포트폴리오 데모용일 뿐 정식 검증 과정이 아니며, WebGL 빌드/확인은 사용자가 명확히 요청할 때만 한다.
**포트폴리오용 프로젝트** — 면접관에게 보여줄 코드이므로 문서는 솔직하게, 코드 품질에 신경 쓰고, 알려진 한계(저장 시스템 미완성 등)는 숨기지 말고 드러낼 것.
**세이브 원칙: 진행도를 유실하지 않는다** — 로드 실패, 형식 변경, 저장 실패는 기존 세이브를 보존하는 쪽으로 처리할 것(아직 못 지키는 부분은 `docs/TODO.md`의 "저장").

## 핵심 게임 루프

`GameInitializer.Start()`(세이브 로드 → `GameSession` 생성 → 플레이어 스폰) → `GameManager.Initialize(session, player)` → `StageManager.Initialize(stage)` → 몬스터 인카운터 반복 스폰(`MonsterSpawner`) → 인카운터 다 죽이면 보스 도전 가능 → 보스 클리어 시 다음 스테이지로 전환. 플레이어는 자동 전투(타겟 자동 탐색 + 공격)만 하며 조작 입력은 없음 — 스탯 업그레이드와 스킬 장착만 플레이어가 직접 함.

## 데이터 아키텍처 (Excel → JSON → ScriptableObject)

```
Excel(.xlsx) → ExcelToJsonConverter(Editor) → JSON(Assets/Resources/Data/*.json)
            → 리플렉션으로 IDataSyncer 구현체 전부 탐색해서 실행
            → *DatabaseSyncer.cs → ScriptableObject(.asset)로 동기화
```

- `IData` 인터페이스(`int Key { get; }`)를 구현하는 데이터 클래스: `MonsterData`, `StageData`, `SkillData`, `PlayerStatData` (전부 `Assets/Scripts/Data/`)
- 각 데이터 클래스는 `[SerializeField] private`필드 + PascalCase 프로퍼티 + `[JsonProperty("camelCase이름")]` 패턴. **`JsonProperty`의 문자열 값이 엑셀 컬럼명/JSON 키이며, C# 멤버 이름과 분리되어 있음** — C# 쪽 이름을 바꿔도 엑셀/JSON 포맷엔 영향 없음.
- `ExcelToJsonConverter`의 컬럼 매칭(`GetFieldIgnoreCase`/`GetPropertyIgnoreCase`)은 필드 → 프로퍼티 순으로 대소문자 무시 매칭을 시도하는 리플렉션 기반이라, 새 데이터 타입을 추가해도 컨버터 자체는 수정할 필요 없음.
- `*DatabaseSyncer.cs`(`Assets/Editor/DataImporter/`)는 `IDataSyncer` 구현체로, 엑셀 재import 시 프리팹/배경텍스처를 자동 매칭하고, `groundOffset` 같은 디자이너가 수동 조정한 값은 보존하면서 데이터만 갈아끼움.

### ⚠️ 직렬화 함정 (실제로 한 번 게임을 망가뜨렸던 이슈)

Unity ScriptableObject(`.asset`)는 **C# 필드 이름을 YAML 키로 그대로 사용**해서 직렬화함. `[JsonProperty]`/프로퍼티는 Newtonsoft.Json(엑셀 변환용)만 보는 것이고 **Unity의 자체 직렬화기는 전혀 모름.**

→ `MonsterData`/`StageData` 등의 `[SerializeField] private` 필드 이름을 바꾸면(예: `_id` → `id`), 기존 `.asset` 파일의 YAML 키(`_id: 1001`)와 더 이상 매칭이 안 돼서 **그 값이 조용히 기본값(0/null)으로 리셋됨.** 컴파일 에러도, 런타임 에러도 안 남고 그냥 데이터가 사라짐.

이게 원인이 돼서 한 번 "캐릭터가 안 달리고 배경도 안 바뀌고 진행이 안 되는" 버그가 났던 적 있음 (`StageDatabase.asset`의 `_stageNumber`가 코드의 `stageNumber`와 안 맞아서 `GetByNumber(1)`이 실패 → `StageManager.Initialize()` 실패 → 당시 `GameManager.Start()`(지금은 `GameManager.Initialize()`) 조기 return → 그 아래 있던 달리기 시작/배경 갱신/스폰 루프가 전부 실행 안 됨).

**필드 이름을 바꿀 일이 있으면**: `[FormerlySerializedAs("옛이름")]`을 붙이거나, 엑셀에서 데이터를 재동기화(Syncer 재실행)하거나, `.asset` YAML을 직접 패치해서 코드와 데이터 양쪽을 같이 맞출 것. 코드만 고치고 끝내면 안 됨.

## FSM 패턴

`Assets/Scripts/FSM/` — `IState`/`StateMachine` 기반. `Player/`, `Monster/` 하위에 각 상태(`PlayerRunState`, `PlayerAttackState`, `PlayerSkillState`, `PlayerDieState`, `BossRunState`, `BossAttackState`, `MonsterIdleState` 등).

- `PlayerCombatState`(추상 베이스)가 `PlayerAttackState`/`PlayerSkillState` 공용으로 `isAnimationFinished` 플래그를 들고 있음. `Enter()` 시 기본 `true`, `DoAction()`에서 새 행동을 막는 게이트로 씀, 실제 공격이 시작될 때만 `false`로 리셋. (애니메이터 `AnyState` 전이나 별도 `hasHitLanded` 플래그 방식은 의도적으로 버린 설계 — 더 단순한 단일 플래그 방식으로 정착함.)

## 매니저/유틸리티

- `GameDatabaseManager` — `MonsterDatabase`/`StageDatabase`/`SkillDatabase`/`BalanceConfig`(SO) 4개를 들고 있는 조회 파사드. `GetMonster(id)`/`GetStage(stageNumber)`/`GetSkill(id)`/`Balance`(스테이지 성장 곡선)로 다른 코드가 SO를 직접 참조하지 않고 항상 이 매니저를 거치게 함. `Instance` getter가 지연 초기화라 다른 매니저의 `Awake()` 실행 순서에 의존하지 않음.
- `PoolManager` — Unity 내장 `UnityEngine.Pool.ObjectPool<T>` 래퍼. 프리팹별로 풀을 따로 관리(`Dictionary<GameObject, IObjectPool<GameObject>>`). 몬스터/보스 스폰에 사용. 스테이지 전환 시 `ClearPool(prefab)`으로 이전 스테이지 몬스터 풀을 정리함.
- `PlayAreaBounds` — 화면 하단 UI 비율(`bottomUiScreenRatio`)을 기준으로 "캐릭터가 서 있을 수 있는 월드 Y(`GroundY`)"를 계산하는 단일 소스. `MonsterSpawner`/`PlayerController`의 스폰 위치 계산이 전부 여기서 나온 값을 씀 — 하단 UI 비율이 바뀌면 이 한 곳만 고치면 됨.
- `CombatRangeUtility` — 사거리 판정용 "반너비" 계산. 중심점 거리만으로 판정하면 덩치 큰 몬스터가 실제 몸 안으로 파고들어야 맞는 문제가 있어서, `SpriteRenderer.bounds`를 우선 쓰고 없으면 `Collider2D.bounds`로 대체. **주의**: 감지용 트리거 콜라이더(예: GroundSensor)가 있는 캐릭터는 `Collider2D`를 먼저 읽으면 그 작은 센서 크기가 잡혀서 반너비가 실제보다 훨씬 작게 계산되는 버그가 있었음 — 그래서 SpriteRenderer를 우선시함.

## 전투/스킬

설계와 검증 결과는 `docs/COMBAT_DESIGN.md`.

- **전투는 고정 타임스텝(0.02초)으로 돈다.** `CombatSimulation`(`Assets/Scripts/Manager/`, 씬에 하나)이 전투에서 유일한 `FixedUpdate`를 갖고, 매 단계 정해진 순서로 진행한다: 몬스터 위치 되돌리기 → `PlayerController.Tick()` → 몬스터 `Tick()` → `BackgroundScroller.Tick()` → 보간용 기록. 플레이어·몬스터·배경에는 `Update`가 없다. 같은 세이브면 30fps, 120fps, 렉이 섞여도 처치 시각이 소수점까지 같다(검증함).
  - **시간 규칙:** 위치를 읽거나 움직이는 전투 로직과 전투 타이머는 `Tick` 안에서만 돈다. 그 안에서 `Time.deltaTime`은 0.02라서 상태 코드는 `Time.deltaTime`을 그대로 쓴다. UI 입력 중 위치를 읽는 것(스킬 슬롯 탭)은 `PlayerController.RequestSkillSlot`으로 다음 단계에 처리한다. `GameManager`의 스폰 루프는 `WaitForFixedUpdate`와 단계 수로 기다린다(`WaitForSeconds`/`yield return null` 쓰지 말 것). 스테이지 전환 페이드만 화면 시간이다.
  - **보간:** 화면 프레임에서는 몬스터 위치와 배경 UV를 직전 두 단계 사이로 그린다(최대 0.02초 늦게 보임). 몬스터를 스폰하거나 위치를 직접 정하면 보간 기준을 맞춰야 한다(`Monster.Initialize`가 함). 안 하면 예전 자리에서 미끄러져 온다.
  - 캐릭터 프리팹 5개의 Animator Update Mode는 **Fixed**다. 타격/동작 끝 애니메이션 이벤트가 고정 단계에서 일어난다.
- **CCD: 살아 있는 몬스터는 플레이어 몸을 통과하지 못한다**(`Monster.moveToPlayer`). 접촉선(플레이어 x + 두 반너비)에서 멈추고, 이미 겹쳐 있으면 더 파고들지 않는다. 한 단계 이동량과 무관하게 성립한다(이동속도 100에서 확인). 예전에는 긴 프레임 한 번에 몬스터가 플레이어 뒤로 순간이동해 진행이 영구히 멈췄다.
- **스크롤(`GameManager.IsScrolling`)의 주인은 `PlayerRunState`다.** `Enter`가 켜고 `Exit`가 끈다. 달리기가 아닌 상태(공격, 수동 스킬 시전, 사망)에서는 배경과 몬스터가 멈춘다.
- `Monster`(베이스) → `BossMonster`(상속) — 일반 몬스터와 보스는 같은 `IHasHp`/`IDamageable` 계약을 따르되 보스만 별도 공격 패턴(FSM의 `BossAttackState`/`BossRunState`)을 가짐.
- `Skill` 런타임 클래스가 `SkillData`(밸런스 수치) + `ISkillBehavior`(실제 효과 실행)를 묶음. `ISkillBehavior` 구현체: `DamageSkillBehavior`/`AoeDamageSkillBehavior`/`HealSkillBehavior` — `SkillEffectType`에 따라 `Skill.CreateBehavior()`에서 분기 생성. 새 효과 타입을 추가하려면 `ISkillBehavior` 구현체 추가 + 그 switch문에 분기 추가.
- 데미지/힐 텍스트는 색으로 구분: 플레이어가 받는 데미지=빨강, 몬스터가 받는 데미지=흰색, 힐="+N" 초록.
- 공격 애니메이션 속도는 `AttackSpeedMultiplier`(Animator float 파라미터)로 `attackClipLength / AttackInterval`을 매 공격마다 다시 계산해서 동기화함(`PlayerController.Attack()`) — 공격속도 업그레이드로 `AttackInterval`이 짧아져도 애니메이션 자체 재생 속도가 못 따라가서 막혀 보이는 문제 때문에 추가됨.
  - 공격 타이머는 이번 단계에 막 넘친 조각만 다음 간격으로 넘긴다(`AttackTimer.NextStart`). 간격 1.0초에서 실제 간격이 정확히 1.0초다.
  - **알려진 한계:** 다음 공격은 애니메이터가 Attack → Idle → Attack을 한 바퀴 돈 뒤에야 시작돼서, 간격 0.1초(공격속도 최대)에서는 실제 간격이 0.14초다(`docs/TODO.md` "전투").

## 엔지니어링 스타일 (이 프로젝트에서 합의된 방향)

- 예: `PlayerCombatState.isAnimationFinished` 단일 플래그로 정착하기 전에, Animator `AnyState` 전이 방식과 별도 `hasHitLanded` 플래그 방식을 둘 다 시도했다가 "주먹구구식"이라는 이유로 명시적으로 반려됨.
- 새 기능을 freestanding `MonoBehaviour`로 만들기보다, 기존 패턴에 맞춰 들어가는 설계를 선호함 (`StageAnnouncement`가 그 사례). 새 팝업의 패턴은 `UIBase` 상속이 아니라 `UIWindow` + Presenter(아래 "UI 구조").

## UI 구조

설계는 `docs/UI_DESIGN.md`가 기준. 지금은 1차 이관 중이라 두 시스템이 공존한다 — **화면 하나의 주인은 하나, 두 시스템을 아는 곳은 `UIRoot` 하나.**

새 구조 (`Assets/Scripts/UI/Core/`) — **새 팝업은 전부 여기로 만든다.**
- `UIRoot` — 조립 지점. 창별 Presenter 생성 함수를 `Bind`로 등록하고, 기존 `UIManager`와 연결(열기 요청, 탭 전환 시 닫기, 뒤로가기 순서)한다.
- `WindowManager` — 로드/스택/정렬(`UISortBand`)/뒤로가기/열린 창 Tick. 열기는 `windows.Open<SkillManageWindow>(new SkillManageArgs())`, 닫힘과 결과는 `await handle.Closed`.
- 창 추가 순서: ① `UIWindow<TArgs>` View(구조는 `Options`로 코드에) + `TArgs : IWindowArgs<그 창>` ② View 인터페이스 + 순수 C# Presenter(구독은 `CompositeDisposable`) ③ 프리팹(Canvas 넣지 않음)을 `Assets/Data/UI/WindowRegistry.asset`에 등록 ④ `UIRoot`에 `Bind`.
- 창은 `Update`를 쓰지 않는다(숨겨도 돈다). 매 프레임 값은 Presenter의 `Tick()`.
- `InputBlocker` — 참조 카운트 입력 차단. **코루틴 지역변수로 `using`하지 말 것**(끊기면 안 풀림, `FadeController` 참고).
- `SkillManageWindow`(View) + `SkillManagePresenter` + `SkillListItem`/`SkillSlotWidget` — 스킬 학습/슬롯 장착. 첫 이관 사례. (프리팹 파일명은 `SkillManagePopup.prefab` 그대로)

기존 구조 (HUD, 탭 — 이관 예정)
- `UIBase`(`Assets/Scripts/UI/UIBase.cs`) — 기존 패널의 베이스, `UIManager`가 생명주기 관리. 팝업용 서브클래스는 없다.
- `UITabPanel : UIBase` — 탭(`TabType`)이 있는 패널 전용 서브클래스(`StatUpgradePanel`, `SkillSlotBar`). `TabType`은 `UIBase`가 아니라 여기로 분리돼 있음 — 탭 없는 UI(`StageAnnouncement` 등)는 `UIBase`를 직접 상속.
- `StatUpgradePanel` + `UpgradeListItem` — 데이터 기반 업그레이드 목록(`PlayerStats.upgradeStats : Dictionary<UpgradeStatType, IUpgradeStat>`로 Strategy 패턴). 새 업그레이드 스탯 추가 시 `IUpgradeStat` 구현체 하나 추가 + 등록만 하면 됨 (`HpUpgradeStat`, `AttackUpgradeStat`, `AttackSpeedUpgradeStat` 참고).

## 이벤트 버스

`GlobalGameEvents`/`GlobalCombatEvents`(`Assets/Scripts/Event/`) — 정적 이벤트 버스로 매니저 간 직접 참조를 끊음. 예: `Monster`는 죽었다는 사실만 `GlobalCombatEvents.OnMonsterDied`로 알리고, 골드/경험치 지급은 `GameManager`가 구독해서 처리 — `Monster`는 보상 지급 방법을 모름.

## 저장 시스템

- **`PlayerSaveData`(최상위, `Assets/Scripts/Player/PlayerSaveData.cs`)와 `PlayerStatsSaveData`(`PlayerStats` 전용 슬라이스)로 분리돼 있음.** `PlayerSaveData`는 `{ stats: PlayerStatsSaveData, gold, currentStageNumber }` 형태 — 저장 파일 전체에 대응. `PlayerStatsSaveData`는 레벨/경험치/업그레이드 횟수/학습 스킬/장착 슬롯처럼 `PlayerStats`가 실제로 읽고 쓰는 것만 담음. 골드/스테이지 번호는 `PlayerStats`가 전혀 쓰지 않는 데이터라(`GameManager`만 씀) 같은 클래스에 두면 안 된다고 판단해서 분리함.
- **진행도의 단일 소유자는 `GameSession`(`Assets/Scripts/Player/GameSession.cs`).** 스탯(`PlayerStats`), 골드(`GoldWallet`), 저장된 스테이지 번호를 한 객체가 갖는다. 저장되는 것은 세션, 저장되지 않는 전투 상태(현재 HP, 쿨다운, FSM)는 캐릭터(`PlayerController`)가 갖는다.
- **생성 순서는 `GameInitializer.Start()`(`Assets/Scripts/Manager/GameInitializer.cs`, `[DefaultExecutionOrder(-1000)]`) 한 곳.** `ISaveStorage.Load()`(예외면 여기서 중단) → `new GameSession(밸런스 데이터)` → `session.Apply(saveData)` → `PlayerSpawner.EnsurePlayer()` → `player.Initialize(session.Stats)` → `GameManager.Initialize(session, player)` → `CombatSimulation.Initialize(player)` → `UIRoot.Initialize(session, 스킬 목록, player)`. 밸런스 데이터(`PlayerStatDatabase`/`PlayerUpgradeConfig`) 참조도 플레이어 프리팹이 아니라 씬의 `GameInitializer`에 있다. Awake가 아니라 Start인 이유: 다른 객체들의 Awake(이벤트 구독, 싱글턴 등록, `PlayAreaBounds` 계산)가 끝난 뒤 연결해야 하기 때문 — "Awake = 자기 자신, Start = 다른 객체와 연결".
- **저장 데이터 변환은 `GameSession.Apply(PlayerSaveData)` / `GameSession.ToSaveData()` 두 곳뿐.** `Apply`는 객체를 새로 만들지 않고 값만 덮어쓴다(세션/Stats/Wallet을 참조하는 쪽이 옛 객체를 붙잡지 않게). 세이브 로드와 이후 서버 롤백(`docs/SERVER_DESIGN.md`)이 같은 경로를 쓴다.
- **저장 백엔드는 `ISaveStorage`로 추상화**(`Assets/Scripts/Player/ISaveStorage.cs`, 최상위 `PlayerSaveData` 기준). `LocalFileSaveStorage`가 현재 구현체(JSON을 `Application.persistentDataPath/player_save.json`에 입출력). `SaveStorageProvider.Current`가 스위치 지점 — 나중에 서버 저장으로 바꿀 때 `ServerSaveStorage : ISaveStorage`를 새로 만들고 `SaveStorageProvider.Current`에 대입하는 한 줄만 바꾸면 됨. `Load()`는 동기(`GameInitializer.Start()`에서 즉시 써야 해서), `Save()`는 `IEnumerator`(코루틴) — 호출부가 기다리지 않는 fire-and-forget이라 서버 지연을 흡수할 수 있게 미리 비동기 모양으로 잡아둔 것.
- **손상된 세이브는 새 데이터로 대신하지 않는다.** `Load()`는 파일이 없을 때만 신규 데이터를 주고, 파일이 있는데 온전히 읽을 수 없으면 예외를 던진다. `GameInitializer`가 잡아서 세션 없이 중단하므로 종료 저장을 포함한 어떤 저장도 원본을 건드리지 않는다. 새 저장소 구현체도 이 규칙을 지킬 것.
- **검사는 `SaveDataValidator`(`Assets/Scripts/Player/SaveDataValidator.cs`) 한 곳.** `JsonUtility`는 없는 필드를 기본값으로 채워 변환 후에는 손상을 알 수 없어서, 변환 전에 `JObject`로 필수 키/타입/하한/슬롯 길이/중복 키를 본다. `Save()`도 같은 검사를 통과한 값만 쓴다(`NaN` 등이면 에러 로그 후 기존 파일 유지). 상한은 보지 않는다(데이터가 줄었을 때 정상 세이브를 막지 않게).
- **`PlayerStats`는 `ISaveStorage`를 모름.** MonoBehaviour가 아니라 코루틴을 못 돌리는 순수 클래스라서, 실제 저장 트리거(코루틴 시작)는 항상 MonoBehaviour 쪽에서 함. `PlayerStats.Upgrade()`/`LearnSkill()`/`EquipSkill()`/`UnequipSkill()`은 데이터만 바꾸고 이벤트(`OnUpgraded`/`OnSkillLearned`/`OnSkillEquipChanged`)만 쏨. **`GameManager`가 이 이벤트들을 `Initialize()`에서 직접 구독해서 `SaveGame()`을 호출**(`PlayerController`는 거치지 않음) — `PlayerController`는 `RefreshEquippedSkills`처럼 자기 런타임 상태와 진짜 관련 있는 구독만 갖고, 저장은 "언제 저장할지"를 실제로 결정하는 `GameManager`의 책임으로 분리함.
- **저장은 `GameManager.SaveGame()` 한 곳에서 `session.ToSaveData()`를 저장.** 스테이지 번호는 `GameManager`가 `OnStageChanged`를 받아 세션에 반영해 둔다. 업그레이드/스킬학습/스킬장착/스테이지전환 등 저장이 일어나는 모든 경로가 이 메서드 하나로 모인다.
- **재화는 `GameNumber`(`Assets/Scripts/Economy/GameNumber.cs`, 내부 `double`).** 골드, 업그레이드 비용, 몬스터 처치 보상. 저장 DTO(`PlayerSaveData.gold`)는 `double`, 데이터 테이블(`MonsterData.goldReward`)은 `int` 그대로 — 구조체를 직렬화 대상에 넣지 않는다(직렬화 함정). 예전 세이브의 정수 `gold`는 `double` 필드로 그대로 읽힌다(확인함).
- ⚠️ **`PlayerSaveData`/`PlayerStatsSaveData`의 필드나 슬롯 수를 바꾸면 기존 세이브가 깨짐** — ScriptableObject `.asset`의 "직렬화 함정"과 같은 종류(`JsonUtility`도 필드 이름으로 매칭). 검사 대상 필드의 이름이나 슬롯 수를 바꾸면 기존 세이브가 **손상으로 차단되고**, 새로 추가한 필드는 검사되지 않아 **조용히 기본값으로 채워진다.** 새 필드를 필수로 검사하려면 버전 변환이 먼저다(`docs/TODO.md` "저장"). 구조를 바꿀 땐 `%LOCALAPPDATA%\..\LocalLow\DefaultCompany\IdleGameProtoType\player_save.json`을 직접 새 스키마로 마이그레이션할 것.
- **`OnApplicationQuit`/`OnApplicationPause(true)`는 `SaveGameImmediate()`로 동기 처리.** `StartCoroutine`으로 예약하면 앱이 곧 종료/일시정지돼서 코루틴이 실행될 프레임을 못 받을 수 있어, 이 두 경로만 `IEnumerator.MoveNext()`를 끝까지 직접 돌려서 저장이 확실히 끝나게 함. (로컬 저장 기준 안전 — 서버 구현체로 바뀌면 응답을 못 기다리고 끊길 수 있는데, 그건 그때 "종료 직전 저장을 어떻게 보장할지" product 차원에서 다시 봐야 함.)
- **자동저장 체크포인트**: 종료/일시정지, 스테이지 전환 완료, 업그레이드, 스킬 학습/장착. 주기적(타이머) 자동저장은 의도적으로 안 둠.
- **스테이지 중간 진행도(인카운터 진행 수)는 저장 안 함** — 의도된 범위 제한. 저장된 스테이지 번호로 복귀하면 그 스테이지를 처음부터(인카운터 1부터) 다시 시작한다. 전투 중 HP/몬스터 상태까지 직렬화하려면 FSM/스포너 상태까지 손대야 해서 범위 밖으로 뺌.
- 세이브 파일 경로: `C:\Users\<user>\AppData\LocalLow\DefaultCompany\IdleGameProtoType\player_save.json`
- **에디터 검증용 세이브 프로필**(`SaveProfile`, `Assets/Scripts/Player/SaveProfile.cs`): 에디터에서만 켤 수 있고, 켜면 세이브 폴더가 `persistentDataPath/profiles/<이름>/`로 바뀐다(형식과 저장 코드는 같음). 값은 `SessionState`라 에디터를 끄면 기본으로 돌아오고, 플레이 중에는 바꿀 수 없다. 메뉴는 `IdleGame/Save Profile/...`, `IdleGame/Reset Save Data`는 지금 켜진 프로필의 세이브를 지운다. 빌드에는 프로필 코드가 없어서 항상 기본 경로를 쓴다.

## 작업 시 주의사항

- **테스트 코드를 새로 작성하지 말 것** — 기존 `Assets/Tests/EditMode/`는 그대로 두되, 명시적 요청 없이 새 테스트를 추가하지 않음.
- **`git commit`은 사용자가 그 턴에 명시적으로 지시했을 때만.**
- 일부 `.cs` 파일은 CP949(EUC-KR) 인코딩 — UTF-8로 Edit/Write하면 한글 주석이 깨질 수 있으니 주의. (직접 새로 작성하는 파일은 UTF-8로 써도 무방, 기존 CP949 파일을 수정할 때만 주의)
- IData 구현 클래스의 필드/프로퍼티를 리네임하는 작업은 반드시 위 "직렬화 함정" 섹션을 먼저 확인하고, `.asset` 데이터와의 정합성을 같이 맞출 것.

## 외부 도구: Unity 에디터 조작

에디터 조작(컴파일 확인, 씬 편집, 플레이 모드 확인, 테스트)은 아래 우선순위로 한다. 기계별 경로(에디터 설치 위치 등)는 이 파일이 아니라 Claude memory에 있다.

1. **실행 중인 에디터에 연결: Unity CLI + Pipeline 패키지** — 사용자가 에디터를 켜 두었고 프로젝트에 `com.unity.pipeline`이 있으면 `unity status`로 연결을 확인하고 `unity command ...`(플레이 모드, C# 실행, 콘솔, 스크린샷)로 실시간 조작한다. **셸을 쓸 수 있으면 MCP보다 CLI를 우선한다**(CLI가 더 많은 기능을 쓴다). 사용법은 `unity-cli` 스킬. **현재 상태: `com.unity.pipeline` 1.0.1 설치됨, 회사 PC에서 켜 둔 에디터 연결 확인**(`unity status`, `script eval`, `console`, `capture editor_window` 동작. Claude 데스크톱 앱의 MSIX 샌드박스에서도 됨). 에디터가 켜져 있으면 배치 모드는 쓸 수 없으므로(프로젝트 잠금) 이 방식을 쓴다.
   - **사용자 세이브 보호(필수):** 켜 둔 에디터는 사용자의 진짜 세이브를 쓴다. 플레이 모드에 들어가기 전에 항상 `SaveProfile.SetActive(SaveProfile.VerificationProfile)`(`claude` 프로필)을 켜고, 진입 후 콘솔에 `[Save] 검증용 프로필 'claude' 사용 중` 경고가 있는지 확인한 뒤 검증한다. 경고가 없으면 즉시 플레이를 끝낸다. 검증이 끝나면 플레이를 나와서 `SaveProfile.SetActive(null)`로 되돌린다.
   - **열린 씬이 MainScene이 아닐 수 있다**(빈 새 씬이 열려 있던 적 있음). 사용자가 연 씬을 바꾸지 말고 `EditorSceneManager.playModeStartScene`에 MainScene을 넣고 플레이한 뒤 `null`로 되돌린다.
   - **자동 반복 측정:** 플레이를 여러 번 돌릴 때는 임시 `[InitializeOnLoad]` 러너가 `SessionState`의 실행 목록을 하나씩 꺼내 플레이하고, 런타임 쪽 임시 스크립트(`#if UNITY_EDITOR`, `RuntimeInitializeOnLoadMethod`)가 `Time.captureDeltaTime`으로 프레임을 고정해 결과를 프로젝트 `Temp/`에 쓰고 플레이를 끝낸다. 전투 고정 타임스텝 검증에 쓴 것이 이 방식이다(스크립트는 지웠다). 플레이 중에는 스크립트를 고치지 않는다(재컴파일이 측정 사이에 끼어든다).
   - **플레이/빌드 후 정리:** TMP 동적 폰트(`Assets/Fonts/MalgunGothic SDF.asset`)가 플레이 중 채운 글자로 바뀌고, Android 빌드 후 `ProjectSettings/UnityConnectSettings.asset`의 `m_Enabled`가 1이 된다. 플레이하면 `persistentDataPath/Unity/`(분석 캐시)도 생긴다. 셋 다 되돌리거나 지운다.
   - **빌드:** 켜 둔 에디터에서는 `unity command build run --target Android --output-path Temp/AndroidBuild/idleproto.apk --confirm true` 후 `build status`로 기다린다. 출력은 프로젝트 안에 둔다(Claude 쪽 AppData 경로는 MSIX 가상화로 예전 파일이 보일 수 있다).
2. **에디터가 꺼져 있으면: 배치 모드** — `Unity.exe`를 창 없이 띄워 일을 시키고 종료한다. 0단계(GameSession/GameInitializer)를 이 방식으로 구현·검증했다.
   - **실행 전:** 사용자가 이 프로젝트를 에디터로 열고 있으면 실행할 수 없다(프로젝트 잠금, `Temp/UnityLockfile`). Unity 프로세스가 없는지 먼저 확인.
   - **컴파일 + 임의 작업:** `Unity.exe -batchmode -quit -projectPath <프로젝트> -executeMethod <클래스.메서드> -logFile <로그>`. 로그에서 `error CS`, `Exception`, `Exiting batchmode successfully now!`를 확인. 한 번에 1~2분 걸린다.
   - **테스트:** `-runTests -testPlatform EditMode -testResults <xml>` (`-quit`를 붙이지 않는다).
   - **씬 편집/확인:** `Assets/Editor/`에 임시 에디터 스크립트를 만들어 `-executeMethod`로 실행하고, 끝나면 스크립트와 `.meta`를 반드시 지운다. 씬 참조 연결은 `SerializedObject`로 한다(씬 YAML 직접 편집 금지).
   - **플레이 모드 확인:** `[InitializeOnLoad]` 임시 스크립트에서 `EditorApplication.EnterPlaymode()/ExitPlaymode()/Exit()`를 쓴다. 플레이 진입 시 도메인 리로드로 정적 변수가 초기화되므로 단계는 `SessionState`에 저장한다. 사용자 세이브(`player_save.json`)가 이미 있으면 덮어쓰지 않도록 중단하고, 확인용으로 만든 세이브는 끝나고 지운다.
   - **실행 후 정리(알려진 부작용):** `git status`로 의도치 않은 변경을 확인할 것.
     - `BackgroundFitter`가 `[ExecuteAlways]`라 배치 모드에서 씬을 저장하면 배경 오브젝트의 `m_LocalScale`이 화면 비율에 맞춰 바뀐다 → 되돌린다.
     - 테스트 실행은 `persistentDataPath`(`LocalLow/DefaultCompany/IdleGameProtoType/`)에 `TestResults.xml`과 `Unity/` 폴더를 남긴다 → 지운다.
     - 로그의 `UnityEditor.Search.SearchDatabase` `ArgumentOutOfRangeException`은 배치 모드 에디터 내부 예외로 프로젝트 코드와 무관하다.
   - 한계: 화면을 볼 수 없다. UI가 제대로 보이는지는 사용자 확인이 필요하다.
3. **Android 실기기 (adb)** — 최종 확인은 Android 빌드를 실기기에 설치해서 한다.
   - **adb는 PC 전체에서 한 버전만 쓴다.** adb 서버는 PC에 하나(포트 5037)이고, 버전이 다른 adb가 명령을 보내면 서버를 끄고 자기 버전으로 다시 띄운다. 그래서 다른 프로젝트(liveeye_kr 웹 개발)와 같은 PC에서 쓰면 서로의 서버를 꺼 버린다. 기준은 **Unity에 딸린 adb**이고, 다른 도구가 쓰는 SDK의 `platform-tools`도 같은 것으로 맞춰 둔다(회사 PC: `~/android-dev/sdk/platform-tools`를 Unity 것으로 교체, 원본은 `platform-tools-37.0.1-backup`). **Unity를 업그레이드하면 adb 버전이 바뀌므로 그쪽 `platform-tools`도 다시 맞춘다.**
   - 설정은 PC별 `.claude/settings.local.json`(원본: claude-sync `setup/`)의 `env`에 있다: `ADB`(Unity의 adb 경로), `ANDROID_SERIAL`(이 프로젝트 테스트 기기). `ANDROID_SERIAL`이 있으면 adb가 `-s` 없이도 그 기기에만 명령을 보낸다. 같은 PC에 다른 프로젝트가 쓰는 기기가 함께 연결돼 있을 수 있으므로 다른 기기에는 설치·실행·입력을 하지 않는다.
   - `adb kill-server`/`start-server`는 권한 규칙으로 막혀 있다(두 프로젝트 모두). 서버를 다시 띄우는 판단은 사용자가 한다.
   - 빌드: 배치 모드 `-buildTarget Android`로 APK만 만들고(Build & Run을 쓰지 않음), 설치는 `"$ADB" install -r <apk>`. 확인은 `screencap`, `logcat -s Unity:*`, `shell input`.
4. **Unity MCP(`mcp__UnityMCP__*`)** — 과거 세션에서 쓰던 방식. 세션에 연결돼 있을 때만 쓸 수 있다. `execute_code`는 종종 mono.exe 경로 길이 문제로 환경적 오류가 나는데, 코드 내용과 무관한 툴 자체 이슈인 경우가 있음 — 의심되면 `return 1+1;` 같은 최소 코드로 먼저 격리 확인.
