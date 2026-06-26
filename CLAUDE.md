# IdleGameProtoType

Unity 2D 방치형(idle) 액션 게임 프로토타입. Unity 6000.4.9f1.
**포트폴리오용 프로젝트** — 면접관에게 보여줄 코드이므로 문서는 솔직하게, 코드 품질에 신경 쓰고, 알려진 한계(저장 시스템 미완성 등)는 숨기지 말고 드러낼 것.

## 핵심 게임 루프

`GameManager.Start()` → `StageManager.Initialize(stage)` → 몬스터 인카운터 반복 스폰(`MonsterSpawner`) → 인카운터 다 죽이면 보스 도전 가능 → 보스 클리어 시 다음 스테이지로 전환. 플레이어는 자동 전투(타겟 자동 탐색 + 공격)만 하며 조작 입력은 없음 — 스탯 업그레이드와 스킬 장착만 플레이어가 직접 함.

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

이게 원인이 돼서 한 번 "캐릭터가 안 달리고 배경도 안 바뀌고 진행이 안 되는" 버그가 났던 적 있음 (`StageDatabase.asset`의 `_stageNumber`가 코드의 `stageNumber`와 안 맞아서 `GetByNumber(1)`이 실패 → `StageManager.Initialize()` 실패 → `GameManager.Start()` 조기 return → 그 아래 있던 달리기 시작/배경 갱신/스폰 루프가 전부 실행 안 됨).

**필드 이름을 바꿀 일이 있으면**: `[FormerlySerializedAs("옛이름")]`을 붙이거나, 엑셀에서 데이터를 재동기화(Syncer 재실행)하거나, `.asset` YAML을 직접 패치해서 코드와 데이터 양쪽을 같이 맞출 것. 코드만 고치고 끝내면 안 됨.

## FSM 패턴

`Assets/Scripts/FSM/` — `IState`/`StateMachine` 기반. `Player/`, `Monster/` 하위에 각 상태(`PlayerRunState`, `PlayerAttackState`, `PlayerSkillState`, `PlayerDieState`, `BossRunState`, `BossAttackState`, `MonsterIdleState` 등).

- `PlayerCombatState`(추상 베이스)가 `PlayerAttackState`/`PlayerSkillState` 공용으로 `isAnimationFinished` 플래그를 들고 있음. `Enter()` 시 기본 `true`, `DoAction()`에서 새 행동을 막는 게이트로 씀, 실제 공격이 시작될 때만 `false`로 리셋. (애니메이터 `AnyState` 전이나 별도 `hasHitLanded` 플래그 방식은 의도적으로 버린 설계 — 더 단순한 단일 플래그 방식으로 정착함.)

## 매니저/유틸리티

- `GameDatabaseManager` — `MonsterDatabase`/`StageDatabase`/`SkillDatabase`(SO) 3개를 들고 있는 조회 파사드. `GetMonster(id)`/`GetStage(stageNumber)`/`GetSkill(id)`로 다른 코드가 SO를 직접 참조하지 않고 항상 이 매니저를 거치게 함. `Instance` getter가 지연 초기화라 다른 매니저의 `Awake()` 실행 순서에 의존하지 않음.
- `PoolManager` — Unity 내장 `UnityEngine.Pool.ObjectPool<T>` 래퍼. 프리팹별로 풀을 따로 관리(`Dictionary<GameObject, IObjectPool<GameObject>>`). 몬스터/보스 스폰에 사용. 스테이지 전환 시 `ClearPool(prefab)`으로 이전 스테이지 몬스터 풀을 정리함.
- `PlayAreaBounds` — 화면 하단 UI 비율(`bottomUiScreenRatio`)을 기준으로 "캐릭터가 서 있을 수 있는 월드 Y(`GroundY`)"를 계산하는 단일 소스. `MonsterSpawner`/`PlayerController`의 스폰 위치 계산이 전부 여기서 나온 값을 씀 — 하단 UI 비율이 바뀌면 이 한 곳만 고치면 됨.
- `CombatRangeUtility` — 사거리 판정용 "반너비" 계산. 중심점 거리만으로 판정하면 덩치 큰 몬스터가 실제 몸 안으로 파고들어야 맞는 문제가 있어서, `SpriteRenderer.bounds`를 우선 쓰고 없으면 `Collider2D.bounds`로 대체. **주의**: 감지용 트리거 콜라이더(예: GroundSensor)가 있는 캐릭터는 `Collider2D`를 먼저 읽으면 그 작은 센서 크기가 잡혀서 반너비가 실제보다 훨씬 작게 계산되는 버그가 있었음 — 그래서 SpriteRenderer를 우선시함.

## 전투/스킬

- `Monster`(베이스) → `BossMonster`(상속) — 일반 몬스터와 보스는 같은 `IHasHp`/`IDamageable` 계약을 따르되 보스만 별도 공격 패턴(FSM의 `BossAttackState`/`BossRunState`)을 가짐.
- `Skill` 런타임 클래스가 `SkillData`(밸런스 수치) + `ISkillBehavior`(실제 효과 실행)를 묶음. `ISkillBehavior` 구현체: `DamageSkillBehavior`/`AoeDamageSkillBehavior`/`HealSkillBehavior` — `SkillEffectType`에 따라 `Skill.CreateBehavior()`에서 분기 생성. 새 효과 타입을 추가하려면 `ISkillBehavior` 구현체 추가 + 그 switch문에 분기 추가.
- 데미지/힐 텍스트는 색으로 구분: 플레이어가 받는 데미지=빨강, 몬스터가 받는 데미지=흰색, 힐="+N" 초록.
- 공격 애니메이션 속도는 `AttackSpeedMultiplier`(Animator float 파라미터)로 `attackClipLength / AttackInterval`을 매 공격마다 다시 계산해서 동기화함(`PlayerController.Attack()`) — 공격속도 업그레이드로 `AttackInterval`이 짧아져도 애니메이션 자체 재생 속도가 못 따라가서 막혀 보이는 문제 때문에 추가됨.

## 엔지니어링 스타일 (이 프로젝트에서 합의된 방향)

- **단일 진실 소스를 선호함.** 여러 곳에 분산된 땜질보다 한 군데를 고치는 근본 수정을 선호. 예: `PlayerCombatState.isAnimationFinished` 단일 플래그로 정착하기 전에, Animator `AnyState` 전이 방식과 별도 `hasHitLanded` 플래그 방식을 둘 다 시도했다가 "주먹구구식"이라는 이유로 명시적으로 반려됨.
- 새 기능을 freestanding `MonoBehaviour`로 만들기보다, 기존 패턴(`UIBase` 상속 등)에 맞춰 들어가는 설계를 선호함 (`StageAnnouncement`가 그 사례).
- 과한 추상화/사전 설계보다 지금 필요한 만큼만 — 가설적 미래 요구사항을 위한 설계를 하지 않음.

## UI 구조

- `UIBase`(`Assets/Scripts/UI/UIBase.cs`) — 모든 UI 패널의 베이스, `UIManager.ShowUI<T>()`/`HideUI<T>()`로 생명주기 관리.
- `UITabPanel : UIBase` — 탭(`TabType`)이 있는 패널 전용 서브클래스(`StatUpgradePanel`, `SkillSlotBar`). `TabType`은 `UIBase`가 아니라 여기로 분리돼 있음 — 탭 없는 UI(`StageAnnouncement` 등)는 `UIBase`를 직접 상속.
- `StatUpgradePanel` + `UpgradeListItem` — 데이터 기반 업그레이드 목록(`PlayerStats.upgradeStats : Dictionary<UpgradeStatType, IUpgradeStat>`로 Strategy 패턴). 새 업그레이드 스탯 추가 시 `IUpgradeStat` 구현체 하나 추가 + 등록만 하면 됨 (`HpUpgradeStat`, `AttackUpgradeStat`, `AttackSpeedUpgradeStat` 참고).
- `SkillManagePopup` + `SkillListItem`/`SkillSlotWidget` — 스킬 학습/슬롯 장착 UI.

## 이벤트 버스

`GlobalGameEvents`/`GlobalCombatEvents`(`Assets/Scripts/Event/`) — 정적 이벤트 버스로 매니저 간 직접 참조를 끊음. 예: `Monster`는 죽었다는 사실만 `GlobalCombatEvents.OnMonsterDied`로 알리고, 골드/경험치 지급은 `GameManager`가 구독해서 처리 — `Monster`는 보상 지급 방법을 모름.

## 저장 시스템 — 알려진 갭 (포트폴리오 디스클로저용)

- `PlayerSaveData`(`Assets/Scripts/Player/PlayerSaveData.cs`)는 레벨/경험치/업그레이드 횟수/학습 스킬/장착 슬롯을 JSON으로 `Application.persistentDataPath/player_save.json`에 저장 가능.
- **하지만 자동저장이 없음.** `Save()`가 호출되는 곳은 `PlayerController.TryUpgrade()`(스탯 업그레이드할 때)와 `PlayerSpawner.ReleasePlayer()`(콘텐츠 전환용으로 만들어졌지만 실제로 어디서도 호출 안 됨) 둘뿐. `OnApplicationQuit`/주기적 자동저장 없음 — 업그레이드를 한 번도 안 하면 저장 파일이 생성되지 않음.
- **골드는 저장 로직이 전혀 없음.** `GoldWallet`(`Assets/Scripts/GoldWallet.cs`)은 `GameManager`가 들고 있는 순수 메모리 클래스이고 직렬화/파일 입출력이 아예 없음 — Play 모드 재시작하면 무조건 0.
- 세이브 파일 경로: `C:\Users\<user>\AppData\LocalLow\DefaultCompany\IdleGameProtoType\player_save.json`

## 작업 시 주의사항

- **테스트 코드를 새로 작성하지 말 것** — 기존 `Assets/Tests/EditMode/`는 그대로 두되, 명시적 요청 없이 새 테스트를 추가하지 않음.
- **`git commit`은 사용자가 그 턴에 명시적으로 지시했을 때만.**
- 일부 `.cs` 파일은 CP949(EUC-KR) 인코딩 — UTF-8로 Edit/Write하면 한글 주석이 깨질 수 있으니 주의. (직접 새로 작성하는 파일은 UTF-8로 써도 무방, 기존 CP949 파일을 수정할 때만 주의)
- IData 구현 클래스의 필드/프로퍼티를 리네임하는 작업은 반드시 위 "직렬화 함정" 섹션을 먼저 확인하고, `.asset` 데이터와의 정합성을 같이 맞출 것.

## 외부 도구

- Unity MCP(`mcp__UnityMCP__*`)로 Unity 에디터 직접 조작 가능 (컴파일 체크, Play 모드 진입, `execute_code`로 런타임 코드 실행 등). `execute_code`는 종종 mono.exe 경로 길이 문제로 환경적 오류가 나는데, 코드 내용과 무관한 툴 자체 이슈인 경우가 있음 — 의심되면 `return 1+1;` 같은 최소 코드로 먼저 격리 확인.
