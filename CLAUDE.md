# IdleGameProtoType

Unity 2D 방치형(idle) 액션 게임 프로토타입. Unity 6000.4.9f1.
**목표 플랫폼은 Android.** 플랫폼 동작과 최종 확인은 Android 빌드(+ adb)로 한다. WebGL은 포트폴리오 데모용일 뿐 정식 검증 과정이 아니며, WebGL 빌드/확인은 사용자가 명확히 요청할 때만 한다.
**포트폴리오용 프로젝트** — 면접관에게 보여줄 코드이므로 문서는 솔직하게, 코드 품질에 신경 쓰고, 알려진 한계(저장 시스템 미완성 등)는 숨기지 말고 드러낼 것.
**세이브 원칙: 진행도를 유실하지 않는다** — 로드 실패, 형식 변경, 저장 실패는 기존 세이브를 보존하는 쪽으로 처리할 것(아직 못 지키는 부분은 `docs/TODO.md`의 "저장").

## 핵심 게임 루프

`GameInitializer.Start()`(세이브 로드 → `GameSession` 생성 → 플레이어 스폰 → 전투 월드와 흐름 생성 → 구독 연결) → `director.Begin(스테이지)`. 흐름은 `EncounterDirector`(아래 "흐름")가 갖는다: 인카운터 반복 → `encountersToComplete`번 깨면 보스 도전 가능 → 도전 → 보스 처치 → 전환(가운데에서 다음 스테이지 입장). 스테이지를 처음부터 시작하는 길은 `EncounterDirector.RestartAt(스테이지)`(전환) 하나다: 아군 전멸·보스 제한 시간 초과 = 같은 스테이지, 클리어 = 다음(마지막이면 그 스테이지), 개발자 도구의 이동과 진행도 교체(`GameManager.LoadSnapshot`)도 이 길. 플레이어는 자동 전투(타겟 자동 탐색 + 공격)만 하며 조작 입력은 없음 — 스탯 업그레이드와 스킬 장착, 보스 도전만 플레이어가 직접 함.

## 데이터 아키텍처 (Excel → JSON → ScriptableObject)

```
Excel(DataTables/GameData.xlsx) → ExcelToJsonConverter(Editor, 메뉴 Tools/Excel to JSON Converter)
            → JSON(Assets/Resources/Data/<시트 이름>.json)
            → 리플렉션으로 IDataSyncer 구현체 전부 탐색해서 실행
            → *DatabaseSyncer.cs → ScriptableObject(.asset)로 동기화 (게임이 읽는 것은 .asset뿐)
```

- **원본은 `DataTables/GameData.xlsx`**(프로젝트 루트, Assets 밖이라 엑셀이 여는 임시 파일을 Unity가 가져오지 않는다). 데이터 시트 5개(`MonsterDatabase`, `StageDatabase`, `SkillDatabase`, `PlayerStatDatabase`, `CharacterDatabase`. 시트 이름 = JSON 파일 이름 = 데이터베이스 에셋 이름)와 `설명` 시트가 있다. 예전 원본 엑셀을 찾지 못해 2026-11-24에 당시 `.asset` 값으로 새로 만들었다(변환을 한 바퀴 돌려 `.asset` 값이 바뀌지 않음을 확인). **데이터를 고칠 때는 엑셀을 고치고 변환한다.** `.asset`의 데이터 값을 직접 고치면 다음 변환에서 엑셀 값으로 덮어써진다.

- `IData` 인터페이스(`int Key { get; }`)를 구현하는 데이터 클래스: `MonsterData`, `StageData`, `SkillData`, `PlayerStatData`, `CharacterData` (전부 `Assets/Scripts/Data/`). 데이터베이스 SO는 공통 접점 `IDataTable`을 구현한다.
- **캐릭터 표(`CharacterData`, D12):** 플레이어 캐릭터(직업)의 사거리와 이동 속도. 레벨에 따라 변하는 HP·공격력·공격 간격은 `PlayerStatData`다. 플레이어는 이 표의 캐릭터(`PlayerSpawner.characterId`, 지금은 1 = 전사)로 스폰하고, 프리팹은 `characterName`으로 자동 연결된다.
- 각 데이터 클래스는 `[SerializeField] private`필드 + PascalCase 프로퍼티 + `[JsonProperty("camelCase이름")]` 패턴. **엑셀 열 이름은 C# 필드 이름**(변환기가 필드 → 프로퍼티 이름 순으로 대소문자 무시 매칭), **JSON 키는 `JsonProperty` 문자열**이다. 지금은 셋이 같다. 필드 이름을 바꾸면 엑셀 열 이름도 같이 바꿔야 한다(안 바꾸면 변환기가 거부한다. 아래 "직렬화 함정"도 같이 볼 것).
- **변환기는 검사를 모두 통과해야만 JSON과 `.asset`을 바꾼다**(`ExcelToJsonConverter.TryReadSheet`, 대화상자 없는 핵심 함수). 거부하는 것: 클래스에 없는 열, 같은 열 이름 두 번, **클래스의 직렬화 필드에 해당하는 열이 없음**(새 필드를 추가하고 엑셀에 열을 안 넣은 경우. 예전에는 조용히 0이 됐다), 형식이 틀린 값(정수 칸의 소수, 없는 enum 값, 숫자 칸의 글자), 같은 키. 빈 칸은 기본값. 리플렉션 기반이라 새 데이터 타입을 추가해도 변환기는 고칠 필요 없음.
- `*DatabaseSyncer.cs`(`Assets/Editor/DataImporter/`)는 `IDataSyncer` 구현체로, 몬스터 프리팹(`monsterName`), 캐릭터 프리팹(`characterName`), 스테이지 배경(`bgTexturePath`)을 자동 매칭하고 **찾지 못하면 기존 연결을 유지**한다(경고). `groundOffset`, 스킬 아이콘·VFX·SFX처럼 엑셀에 없는 값은 보존하면서 데이터만 갈아끼움.

### ⚠️ 직렬화 함정 (실제로 한 번 게임을 망가뜨렸던 이슈)

Unity ScriptableObject(`.asset`)는 **C# 필드 이름을 YAML 키로 그대로 사용**해서 직렬화함. `[JsonProperty]`/프로퍼티는 Newtonsoft.Json(엑셀 변환용)만 보는 것이고 **Unity의 자체 직렬화기는 전혀 모름.**

→ `MonsterData`/`StageData` 등의 `[SerializeField] private` 필드 이름을 바꾸면(예: `_id` → `id`), 기존 `.asset` 파일의 YAML 키(`_id: 1001`)와 더 이상 매칭이 안 돼서 **그 값이 조용히 기본값(0/null)으로 리셋됨.** 컴파일 에러도, 런타임 에러도 안 남고 그냥 데이터가 사라짐.

이게 원인이 돼서 한 번 "캐릭터가 안 달리고 배경도 안 바뀌고 진행이 안 되는" 버그가 났던 적 있음 (`StageDatabase.asset`의 `_stageNumber`가 코드의 `stageNumber`와 안 맞아서 `GetByNumber(1)`이 실패 → 당시의 `StageManager.Initialize()` 실패 → `GameManager.Start()` 조기 return → 그 아래 있던 달리기 시작/배경 갱신/스폰 루프가 전부 실행 안 됨. 지금은 `GameInitializer`가 저장된 스테이지를 먼저 확인하고 에러를 낸다).

- **값을 바꿔 보기는 튜닝 덮어쓰기로**(메뉴 `IdleGame/Data/Tuning Overrides`, `docs/COMBAT_ARCHITECTURE.md` 8-7 D15). "표 / 키 / 열 / 값"을 적으면 에디터 플레이에서만 데이터베이스 사본(`DataTuning.Prepare`)에 덮어쓴다. 엑셀과 `.asset`은 그대로이고, 목록은 내 PC 전용 파일(`UserSettings/IdleGame/TuningOverrides.asset`)이다. 켜져 있으면 플레이 시작 때 경고하고, 빌드에는 이 코드가 없다(빌드 전에 남은 항목을 경고). 플레이 중에 바꾸면 바로 다시 적용된다(플레이어 즉시, 몬스터는 다음 스폰). 정한 값은 엑셀로 옮긴다. **Claude가 검증하려고 값을 바꿀 때도 엑셀이 아니라 이것을 쓰고, 끝나면 지운다.**

**필드 이름을 바꿀 일이 있으면**: `[FormerlySerializedAs("옛이름")]`을 붙이거나, 엑셀 열 이름을 같이 바꾸고 재동기화(Syncer 재실행)하거나, `.asset` YAML을 직접 패치해서 코드와 데이터 양쪽을 같이 맞출 것. 코드만 고치고 끝내면 안 됨.

## 전투원 모델 (`docs/COMBAT_ARCHITECTURE.md` 8절)

- **플레이어, 일반 몬스터, 보스는 모두 `Combatant`(순수 C#, `Assets/Scripts/Combat/`) 하나다.** 차이는 데이터다: 공격 간격 > 0이면 기본 공격, `skillId`가 있으면 스킬, 몬스터 표의 `movePolicy`(Hold: 제자리 / Approach: 자기 `moveSpeed`로 다가옴)가 이동 방식. 기본 공격도 스킬도 없으면 교전하지 않는다(지금의 일반 슬라임). 엑셀에서 슬라임의 공격 간격을 넣으면 코드 수정 없이 공격한다(슬라임 프리팹에 공격 동작과 시간표를 미리 둠).
- **호스트(MonoBehaviour)는 그리기와 Unity 연결만 한다.** 공통 기반 `CombatantHost`(뷰, 시간표, 판정 몸, 위치 보간, HP 상태 `Hp` — 머리 위 바와 HUD가 같이 구독, D29) ← `PlayerController`(진행도의 `CombatStats`와 캐릭터 표 → 능력치, UI 입구), `Monster`(몬스터 표 × 스테이지 배율 → 능력치, 보상, 풀). `BossMonster`는 없다: 보스인지는 스폰할 때 정하는 역할이다(흐름이 어느 전투원이 보스인지 안다). `Combatant` → 호스트 알림은 `ICombatantHost` 하나로 모았다(5단계에서 이벤트로 분리).
- **`CombatWorld`(순수 C#)가 전투원 전체와 규칙을 갖는다.** 처리 순서는 아군 → 적, 각 팀 안에서는 등록 순이다(플레이어는 시작 때, 몬스터는 스폰 때 등록하고 정리할 때 해제). 처리 도중의 등록·해제는 그 국면이 끝난 뒤에 반영한다.
- **거리 규칙 하나: 표면 거리**(`CombatWorld.Gap` = 중심 거리 − 두 반너비, 겹치면 음수). 교전, 단일 대상, 범위 스킬 반경이 모두 이것이다. **대상은 반대 팀의 살아 있는 가장 가까운 전투원**(앞뒤 무관, 같은 거리면 등록 순)이고, 지금 대상이 유효하면 바꾸지 않는다.
- **판정 몸은 프리팹의 `HitboxProfile`**: 중심 = 피벗 + `bodyOffset.x`, `halfWidth` > 0 필수(스프라이트 바운드 자동 계산은 없앴다). 판정과 이펙트 위치가 같은 몸을 쓴다.
- **상태는 단계 값 하나**(`CombatantPhase`: Inactive / Moving / Engaged / Dead). 상태 객체와 `StateMachine`은 없다. 이동 ↔ 교전 전환은 양쪽이 같은 판정(`TryAcquireTarget`)을 써서 떨지 않는다. 사망 시간표가 있으면(플레이어) 그것이 끝날 때 호스트에 알리고(`HandleDeathCompleted` → 재시작 요청), 없으면 바로 알린다.
- **HP·데미지는 `GameNumber`**(D5, D14). 데미지는 `GameNumber.Round(공격력 × 배율)`, 몬스터 성장 배율은 `Math.Pow`(double). `PlayerStats.CombatStats`의 최대 HP·공격력도 `GameNumber`다(업그레이드 표시 API `IUpgradeStat`는 아직 float, 탭 이관 때 정리).
- **전투 진행 중 할당 0**(R6): 상태 객체 없음, 범위·스킬 결과는 재사용 버퍼, 몬스터의 `Combatant`는 풀 객체마다 하나를 다시 쓴다(`Spawn`이 이전 상태를 지움). `CombatAllocationTests`가 확인한다.
- **데이터와 프리팹의 짝은 에디터·빌드가 검사한다**(`CombatDataValidator`, `ActionTimelineTools.ValidateCombatData`에서 호출): 몬스터의 공격 간격 > 0이면 프리팹 시간표에 `Attack`, `skillId`의 스킬과 그 행동 키, 캐릭터 프리팹의 `Attack`·`Death`와 배울 수 있는 모든 스킬의 행동 키, 스테이지가 가리키는 몬스터, 모든 전투원 프리팹의 판정 몸·시간표·뷰.

## 흐름 (`docs/COMBAT_ARCHITECTURE.md` 10절, 4-A)

- **`EncounterDirector`(순수 C#, `Assets/Scripts/Flow/`)가 인카운터·보스·스테이지 흐름 전체를 갖는다.** 고정 단계 ①에서 돌고, 지난 단계의 결과(보스 처치, 아군 전멸)를 월드에 묻는다. 전투 알림(사망 등)은 흐름을 부르지 않는다(재진입 없음). `StageManager`, `GlobalGameEvents`, `GameManager`의 흐름 코루틴은 없어졌다.
- **상태는 단계 값 하나**(`FlowPhase`: Starting → Encounter → EncounterCleared → … → BossIntro → Boss → Transition). 대기는 끝나는 단계 번호로 저장한다. 같은 단계에 보스 처치와 전멸이 보이면 클리어가 먼저다.
- **명령은 검사·결과는 즉시, 실행은 다음 ①**(D23): `RequestBossChallenge()`(Accepted/NotAvailable/Busy), `RestartAt(스테이지)`(Accepted/Busy/NoSuchStage). 보스 버튼, 개발자 도구, `LoadSnapshot`이 모두 이것만 부른다. 사건을 받는 쪽이 명령을 불러도 그 사건 안에서는 실행되지 않는다.
- **바깥으로 내는 것:** 상태 `Phase`, `CurrentStageNumber`(0 = 시작 전), `BossChallengeAvailable`(도전을 받거나 전환이 시작되면 꺼진다), 사건 `StageEntered(스테이지, 이유)`(`GameManager`가 세션 스테이지 번호 + 저장, 시작은 저장 안 함), 매 프레임 값의 원인 `Transition`(시각), `BossDeadline`, 경고 `Stalled`(진전 감시: 게임 시간 30초 동안 피해·사망·이동·흐름 변화가 없으면, 에디터·개발 빌드).
- **스폰 규칙(원점 재설정, 위치, 몇 마리)은 디렉터, 뷰는 `IEncounterHost`**(`MonsterSpawner`: 풀에서 꺼내 능력치를 넣은 전투원을 돌려주고, 스테이지를 떠날 때 풀을 비운다). 인카운터의 끝은 "살아 있는 적이 없다"(월드에 묻는다).
- **흐름 시간은 데이터:** 정리 대기·보스 등장 대기·전환 길이는 `BalanceConfig`(D24), 보스전 제한 시간은 스테이지 표 `bossTimeLimit`(0 = 없음, 넘기면 전멸과 같다, D25). 0이면 그 대기가 없다(같은 ①에서 다음 단계로). 대기는 "n × 단계 ≥ 대기"인 가장 작은 n 단계라서, **흐름 시간 데이터는 double**이다(float면 0.1 같은 값이 한 단계 늘어난다).
- 테스트는 `EncounterDirectorTests`(진짜 `CombatWorld`·`Combatant` + 가짜 호스트): 정확한 대기, 명령 결과, 전멸·클리어 순서, 결정성, 무작위 데이터 장시간 진행, 할당 0.

## 매니저/유틸리티

- `GameDatabaseManager` — `MonsterDatabase`/`StageDatabase`/`SkillDatabase`/`CharacterDatabase`/`BalanceConfig`(SO)를 들고 있는 조회 파사드(에디터 플레이에서는 튜닝 덮어쓰기를 적용한 사본). `GetMonster(id)`/`GetStage(stageNumber)`/`GetSkill(id)`/`GetCharacter(id)`/`Balance`(스테이지 성장 곡선)로 다른 코드가 SO를 직접 참조하지 않고 항상 이 매니저를 거치게 함. `Instance` getter가 지연 초기화라 다른 매니저의 `Awake()` 실행 순서에 의존하지 않음.
- `PoolManager` — Unity 내장 `UnityEngine.Pool.ObjectPool<T>` 래퍼. 프리팹별로 풀을 따로 관리(`Dictionary<GameObject, IObjectPool<GameObject>>`). 몬스터/보스 스폰에 사용. 다른 스테이지에 들어갈 때 `MonsterSpawner.ReleaseStage`가 `ClearPool(prefab)`으로 이전 스테이지 몬스터 풀을 정리함.
- `PlayAreaBounds` — 화면 하단 UI 비율(`bottomUiScreenRatio`)을 기준으로 "캐릭터가 서 있을 수 있는 월드 Y(`GroundY`)"를 계산하는 단일 소스. `MonsterSpawner`/`PlayerController`의 스폰 위치 계산이 전부 여기서 나온 값을 씀 — 하단 UI 비율이 바뀌면 이 한 곳만 고치면 됨.

## 전투/스킬

목표 구조와 단계는 `docs/COMBAT_ARCHITECTURE.md`(1~3단계와 4-A(흐름) 완료, 4-B·5단계 남음). 고정 타임스텝 수정의 설계·검증 기록은 `docs/COMBAT_DESIGN.md`.

- **전투 규칙을 애니메이션이 정하지 않는다.** 행동의 길이는 게임 데이터가, 타격 시점(행동 안의 비율)은 행동 시간표가 정하고, 전투 로직은 애니메이션 이벤트와 애니메이터 전이를 하나도 읽지 않는다. 애니메이터를 꺼도 처치 시각이 같다(검증함).
  - **행동 시간표**(`ActionTimeline`, 캐릭터마다 `ActionTimelineSet` 에셋, `Assets/Data/Combat/Timelines/`): 키(= 애니메이터 상태 이름), 클립, 그리고 클립에서 구운 값(클립 길이, 타격 비율들). 타이밍은 스킬이 아니라 **시전자의 애니메이션**에 속한다(같은 스킬 1번을 플레이어와 BossSlime이 다른 클립으로 쓴다). 스킬의 키는 `SkillData.AnimationTrigger`(이름은 옛 그대로, 의미는 "행동 키").
  - **굽기:** 클립의 타격 표식 이벤트 `AE_OnHit`(여러 개 가능, 사망 클립에는 없음)을 메뉴 `IdleGame/Combat/Bake Action Timelines`가 비율로 굽는다. 표식은 이것 하나뿐이고, `AE_`로 시작하는 다른 이벤트(옛 해제 표식 `AE_OnAttackEnd`/`AE_OnDeathEnd` 포함)가 있으면 굽기가 거부한다. **클립을 고치면 다시 구워야 한다.** 구운 뒤 클립이 바뀌었거나 스킬 시전 시간이 0 이하면 에디터가 열릴 때 에러를 내고, 빌드는 실패한다(`ActionTimelineTools.ValidateCombatData`). 표식 이벤트는 런타임에 `CharacterAnimationView`의 빈 메서드가 받는다.
  - **행동 길이는 데이터:** 기본 공격 = 공격 간격(플레이어 `PlayerStats.AttackInterval`, 보스 `MonsterData.AttackInterval`), 스킬 = `SkillData.CastTime`(시전자와 무관하게 같은 값), 사망 = 사망 클립 길이. 클립은 그 길이에 맞춰 재생되고 애니메이션은 비율만 정한다(아트를 바꿔도 밸런스가 안 바뀐다). `castTime`의 원본은 엑셀 `SkillDatabase` 시트다(`docs/COMBAT_ARCHITECTURE.md` 7-13).
- **시간 모델: 시계 하나, 절대 시각, 시간 순서 사건 처리**(`docs/COMBAT_ARCHITECTURE.md` 7-11).
  - `SimClock`(순수 C#)이 전투의 유일한 시계다. 모든 시각은 절대 시각(초, double)이고, 단계 경계는 `단계 번호 × 단계 길이`. **전투 코드는 `Time.deltaTime`을 쓰지 않는다.** 시각과 단계 길이는 시계에서 받는다.
  - **시간을 갖는 상태는 남은 시간이 아니라 절대 시각으로 저장한다.** 행동은 시작·끝·표식 시각, 스킬은 준비 시각(`Skill.ReadyAt`). **시각은 한 번 계산해 저장하고 다시 계산하지 않는다**(같은 시각 비교가 정확히 같다. 여유값(epsilon)을 두지 않는다).
  - `CombatScheduler`가 한 단계 [t0, t1] 안에서 교전 중인 모든 전투원의 사건을 시각 순서로 처리한다: 가장 이른 사건 시각으로 시계를 옮기고 → 그 시각의 표식을 모두 처리(타격 등) → 모두 결정. 같은 시각이면 아군 → 적, 각 팀은 등록 순. 사건 중 죽은 전투원은 뒤 사건이 없다. 전투원당 단계 사건 상한 256, 넘으면 에러.
  - 사건 = 행동 표식(타격, 완료) + 행동이 없을 때 **결정 입력이 바뀌는 시각**(`ICombatActor.NextDecisionInputChange`: 지금은 장착 스킬 준비 시각, 그리고 수동 요청의 준비 시각). 시간에 따라 바뀌는 결정 입력이 늘면 그 함수에 더한다. 반복문에 사례를 끼워 넣지 않는다.
- **`ActionRunner`(순수 C#)가 행동 하나의 표식 시각을 갖고, `CombatActions`(전투원마다 하나)가 다음 행동을 정한다.** 시전자 계약은 `ICombatActor`(`Combatant`가 구현, 테스트는 가짜 시전자).
  - 기본 공격끼리는 앞 공격이 **완료**된 시각에 이어진다. 그래서 공격 간격이 데이터 값 그대로다(실측: 간격의 float 값과 비트 단위로 같음, 타격 시각은 단계 경계와 무관).
  - **취소 없음**(`docs/COMBAT_ARCHITECTURE.md` 7-13): 모든 행동(기본 공격, 스킬)은 끝까지 하고, 다음 행동은 지금 행동이 **완료**된 시각에 정한다(수동 요청 → 슬롯 순서 → 기본 공격). 어떤 행동도 다른 행동을 끊지 못하고, 행동을 끊는 것은 사망과 교전 종료뿐이다. 쿨다운은 시전 시작 시각부터.
  - 타격 순간에 대상을 다시 확인한다. 죽었거나 사거리 밖이면 사거리 안에서 다시 찾고, 없으면 헛친다.
  - 수동 스킬(스킬 슬롯 탭)은 요청으로 남았다가, 준비돼 있고 사거리 안에 대상이 있는 다음 결정 시점에 쓴다. 달리는 중에 누르면 교전이 시작될 때 쓴다.
  - 교전은 전투원의 단계 값(`Engaged`)이고, 그 전환은 `Engage`/`Disengage` 표시만 하며 시간을 진행하지 않는다.
- **스킬 쿨다운은 스킬 id에 붙는다**(`SkillLoadout`). 슬롯을 옮기거나 장착을 풀었다 껴도 유지되고, 준비 시각이 절대 시각이라 장착하지 않은 동안에도 흐른다(`Tick` 없음). UI는 `PlayerController.GetSkillCooldownRatio`(시계 기준)로 표시한다. 저장하지는 않는다.
- **애니메이터는 재생기다**(`CharacterAnimationView`). 컨트롤러는 조건 전이 없는 상태 모음이고, 상태를 이름으로 `Play`/`CrossFade`한다. 행동 상태(Attack, Skill, Healing)의 속도는 `ActionSpeed` 파라미터(`클립 길이 ÷ 실제 길이`). Animator Update Mode는 Normal.

- **전투는 고정 타임스텝(0.02초)으로 돈다.** `CombatSimulation`(`Assets/Scripts/Manager/`, 씬에 하나)이 전투에서 유일한 `FixedUpdate`를 갖고, 매 단계 국면 순서로 진행한다: ① 흐름(`EncounterDirector.Tick`) → ② 전투 사건(`CombatWorld.RunEvents` → `CombatScheduler`) → ③ 단계 전이와 이동(`CombatWorld.StepPhases`, 아군 → 적) → ⑤ 보간용 기록(`CombatWorld.CaptureStep`), 시계 진행. (④ 배경은 3단계에서 없어졌다.) 전투원·배경에는 `Update`가 없다. 같은 세이브면 30fps, 120fps, 렉이 섞여도, 애니메이터를 꺼도 처치·타격 시각과 스테이지 입장 시각이 같다(검증함).
  - **단계 길이는 설정값 그대로(0.02):** Unity 6은 고정 단계를 유리수로 저장해서 `Time.fixedDeltaTime`이 0.0199999921로 들어온다. 시계는 `CombatSimulation.StepLengthOf`(유효숫자 6자리)로 설정이 뜻하는 값을 쓴다. 안 그러면 데이터의 초가 단계 수와 맞지 않는다(0.5초 대기가 26단계).
  - **시간 규칙:** 위치를 읽거나 움직이는 전투 로직과 전투 시간은 고정 단계 안에서만 돈다. 행동·쿨다운은 ②에서 단계 안의 정확한 시각으로, 이동과 달리기↔교전 전환은 ③에서 단계 단위로(위치를 단계마다 적분하는 모델. 교전 시작을 단계 안의 접촉 시각으로 정밀화하는 것은 하지 않기로 했다, D19). UI 입력 중 위치를 읽는 것(스킬 슬롯 탭)은 `PlayerController.RequestSkillSlot`으로 다음 단계에 처리한다. 흐름(스폰, 대기, 스테이지 전환)도 ①의 시뮬레이션 시간이다. 페이드는 그 시간을 그리기만 한다(배속에서 같이 빨라진다). 화면 시간(`unscaledDeltaTime`)은 UI 애니메이션(스테이지 안내 등)뿐이다. 코루틴으로 흐름을 기다리지 말 것.
  - **보간:** 위치의 주인은 `Combatant.X`(판정 몸 중심, 월드 x)이고, 화면 프레임에서는 호스트(`CombatantHost.ApplyRenderPose`)가 직전 두 단계 사이를 그린다(최대 0.02초 늦게 보임). 위치를 직접 정할 때는 `Combatant.Place`/`Spawn`을 쓴다(보간 기준도 같이 맞춘다). 안 하면 예전 자리에서 미끄러져 온다.
- **공간은 월드 x + 카메라다**(3단계, `docs/COMBAT_ARCHITECTURE.md` 9절). 플레이어(전진 정책)가 이동 단계에서 실제로 앞(+x)으로 가고, `CameraRig`(Main Camera, LateUpdate)가 처음의 카메라-플레이어 거리를 유지하며 따라간다. 일반 몬스터(Hold)는 움직이지 않고, 보스(Approach)는 자기 `moveSpeed`로 다가온다. 그래서 시체·동전·이펙트처럼 가만히 있는 것은 아무 처리 없이 화면에서 지나간다. "스크롤" 개념(`IsScrolling`, `OnScrollChanged`)은 없다. 배경(`BackgroundScroller.Follow`)은 카메라를 따라 화면을 덮고, 텍스처는 월드 이동 거리 × `scrollScale`만큼 흐른다.
- **이동은 sweep 하나**(`Combatant.Sweep`, `CombatWorld.FindBlockingContact`): 그 방향의 가장 가까운 살아 있는 상대 몸(접촉선)까지만 간다. 이미 겹쳐 있으면 더 파고들지 않는다. 한 단계 이동량과 무관하게 성립한다(이동 속도 1e6에서 테스트). 시체와 뒤쪽 적은 막지 않는다. 예전에는 긴 프레임 한 번에 몬스터가 플레이어 뒤로 순간이동해 진행이 영구히 멈췄다.
- **원점 재설정(D16):** 인카운터·보스를 스폰할 때마다(`EncounterDirector`, 국면 ①) 대열 앞장의 위치 정수 부분만큼 모든 좌표를 뺀다(`CombatWorld.RecenterOn`/`ShiftOrigin`, 월드 처리 밖에서만, 처리 중이면 예외). 누적량(`TotalShift`)으로 배경이 이어지고, 살아 있는 연출(동전, 이펙트, 데미지 숫자 기준점)은 `CombatEffectManager.ShiftOrigin`이 옮긴다. 그래서 이펙트 프리팹의 파티클은 로컬 시뮬레이션 공간이어야 한다(데이터 검사가 잡는다). 데미지 숫자는 월드 기준점을 매 프레임 화면 좌표로 바꿔 월드를 따른다.
- **스폰 위치는 데이터:** 첫 몬스터 피벗 x = 대열 앞장 몸 중심 + 스테이지 표 `spawnDistance`, 둘째부터 `monsterSpacing` 간격. 화면 크기와 무관해서 기기 화면비가 전투를 바꾸지 않는다(D18).

- `Skill` 런타임 클래스가 `SkillData`(밸런스 수치) + `ISkillBehavior`(실제 효과 실행, `Execute(시전자, 대상, 맞은 전투원 버퍼)`)를 묶음. `ISkillBehavior` 구현체: `DamageSkillBehavior`/`AoeDamageSkillBehavior`/`HealSkillBehavior` — `SkillEffectType`에 따라 `Skill.CreateBehavior()`에서 분기 생성. 새 효과 타입을 추가하려면 `ISkillBehavior` 구현체 추가 + 그 switch문에 분기 추가.
- 데미지/힐 텍스트는 색으로 구분(팀 기준): 아군(플레이어)이 받는 데미지=빨강, 적이 받는 데미지=흰색, 힐="+N" 초록.

## 엔지니어링 스타일 (이 프로젝트에서 합의된 방향)

- 예: 공격이 끝났는지를 애니메이션이 알려 주던 시절, Animator `AnyState` 전이 방식과 별도 `hasHitLanded` 플래그 방식을 시도했다가 "주먹구구식"이라는 이유로 반려하고 `isAnimationFinished` 단일 플래그로 정착했었다. 2026-11-24에는 그 축 자체를 바꿔 행동의 길이를 게임 데이터가 정하고 애니메이션이 따라오게 했다(`docs/COMBAT_ARCHITECTURE.md` 4절, 7-13).
- **지금 밸런스 수치는 기준이 아니다.** 구조가 단단하고, 밸런스를 데이터만 고쳐서 바꿀 수 있는지가 기준이다. 검증은 지금 값에서 통과하는지가 아니라 불변식과 극단값으로 한다.
- 새 기능을 freestanding `MonoBehaviour`로 만들기보다, 기존 패턴에 맞춰 들어가는 설계를 선호함 (`StageAnnouncement`가 그 사례). 새 팝업의 패턴은 `UIBase` 상속이 아니라 `UIWindow` + Presenter(아래 "UI 구조").

## UI 구조

설계는 `docs/UI_DESIGN.md`가 기준. 지금은 1차 이관 중이라 두 시스템이 공존한다 — **화면 하나의 주인은 하나, 두 시스템을 아는 곳은 `UIRoot` 하나.**

새 구조 (`Assets/Scripts/UI/Core/`) — **새 팝업은 전부 여기로 만든다.** (예외: 개발자 도구 패널은 IMGUI, 아래 "개발자 도구")
- `UIRoot` — 조립 지점. 창별 Presenter 생성 함수를 `Bind`로 등록하고, 기존 `UIManager`와 연결(열기 요청, 탭 전환 시 닫기, 뒤로가기 순서)한다.
- `WindowManager` — 로드/스택/정렬(`UISortBand`)/뒤로가기/열린 창 Tick. 열기는 `windows.Open<SkillManageWindow>(new SkillManageArgs())`, 닫힘과 결과는 `await handle.Closed`.
- 창 추가 순서: ① `UIWindow<TArgs>` View(구조는 `Options`로 코드에) + `TArgs : IWindowArgs<그 창>` ② View 인터페이스 + 순수 C# Presenter(구독은 `CompositeDisposable`) ③ 프리팹(Canvas 넣지 않음)을 `Assets/Data/UI/WindowRegistry.asset`에 등록 ④ `UIRoot`에 `Bind`.
- 창은 `Update`를 쓰지 않는다(숨겨도 돈다). 매 프레임 값은 Presenter의 `Tick()`.
- `InputBlocker` — 참조 카운트 입력 차단. **코루틴 지역변수로 `using`하지 말 것**(끊기면 안 풀림, `FadeController` 참고).
- `SkillManageWindow`(View) + `SkillManagePresenter` + `SkillListItem`/`SkillSlotWidget` — 스킬 학습/슬롯 장착. 첫 이관 사례. (프리팹 파일명은 `SkillManagePopup.prefab` 그대로)
- **씬의 HUD 위젯도 View + Presenter**(창이 아니라 `WindowManager`를 거치지 않는다): `BossChallengeButton`(보이기 = `BossChallengeAvailable`, 누르면 `RequestBossChallenge`), `StageAnnouncement`(스테이지 번호가 바뀌면 "Stage N"). Presenter는 `Assets/Scripts/UI/Flow/`, `UIRoot.BindHudWidgets`가 만들고 수명을 갖는다. 위젯이 스스로 숨거나 게임 쪽을 직접 부르지 않는다.
- `FadeController` — 흐름의 전환 시각(`EncounterDirector.Transition`)을 그리기만 한다(시작 0 → 가운데 1 → 끝 0). 전환 중에는 입력을 막는다.

기존 구조 (HUD, 탭 — 이관 예정)
- `UIBase`(`Assets/Scripts/UI/UIBase.cs`) — 기존 패널의 베이스, `UIManager`가 생명주기 관리. 팝업용 서브클래스는 없다. `UIHud`는 이제 `PlayerHud`뿐이고 조립이 끝나면 `UIRoot`가 켠다(4-B에서 Presenter로 바뀌면 없어진다).
- `UITabPanel : UIBase` — 탭(`TabType`)이 있는 패널 전용 서브클래스(`StatUpgradePanel`, `SkillSlotBar`). `TabType`은 `UIBase`가 아니라 여기로 분리돼 있음 — 탭 없는 UI(`StageAnnouncement` 등)는 `UIBase`를 직접 상속.
- `StatUpgradePanel` + `UpgradeListItem` — 데이터 기반 업그레이드 목록(`PlayerStats.upgradeStats : Dictionary<UpgradeStatType, IUpgradeStat>`로 Strategy 패턴). 새 업그레이드 스탯 추가 시 `IUpgradeStat` 구현체 하나 추가 + 등록만 하면 됨 (`HpUpgradeStat`, `AttackUpgradeStat`, `AttackSpeedUpgradeStat` 참고).

## 이벤트 버스

`GlobalCombatEvents`(`Assets/Scripts/Event/`) — 전투 → 연출·보상 방향의 정적 이벤트 버스로 매니저 간 직접 참조를 끊음. 예: `Monster`는 죽었다는 사실만 `GlobalCombatEvents.RaiseMonsterDied`로 알리고, 골드/경험치 지급은 `GameManager`가 `MonsterDied`를 구독해서 처리 — `Monster`는 보상 지급 방법을 모름. 사건은 `Subject`(`TargetDamaged`, `MonsterDied`, `Healed`, 내용은 구조체라 발행에 할당 없음)이고 구독은 `IDisposable`로 해제한다. 플레이를 시작할 때 구독자를 비운다(`ResetSubscribers`, UI_DESIGN 5-3).
흐름의 상태와 사건(스테이지, 보스 도전 가능, 입장)은 정적 버스가 아니라 `EncounterDirector`의 반응형 값이다. 예전의 `GlobalGameEvents`(보스 처치 → 클리어 → 전환으로 이어지던 정적 사건 사슬, `OnGameStarted`)는 4-A에서 지웠다.

## 저장 시스템

- **`PlayerSaveData`(최상위, `Assets/Scripts/Player/PlayerSaveData.cs`)와 `PlayerStatsSaveData`(`PlayerStats` 전용 슬라이스)로 분리돼 있음.** `PlayerSaveData`는 `{ stats: PlayerStatsSaveData, gold, currentStageNumber }` 형태 — 저장 파일 전체에 대응. `PlayerStatsSaveData`는 레벨/경험치/업그레이드 횟수/학습 스킬/장착 슬롯처럼 `PlayerStats`가 실제로 읽고 쓰는 것만 담음. 골드/스테이지 번호는 `PlayerStats`가 전혀 쓰지 않는 데이터라(`GameManager`만 씀) 같은 클래스에 두면 안 된다고 판단해서 분리함.
- **진행도의 단일 소유자는 `GameSession`(`Assets/Scripts/Player/GameSession.cs`).** 스탯(`PlayerStats`), 골드(`GoldWallet`), 저장된 스테이지 번호를 한 객체가 갖는다. 저장되는 것은 세션, 저장되지 않는 전투 상태(현재 HP, 쿨다운, 전투 단계)는 전투원(`Combatant`, 호스트 `PlayerController`)이 갖는다.
- **생성 순서는 `GameInitializer.Start()`(`Assets/Scripts/Manager/GameInitializer.cs`, `[DefaultExecutionOrder(-1000)]`) 한 곳.** `ISaveStorage.Load()`(예외면 여기서 중단) → `new GameSession(밸런스 데이터)` → `session.Apply(saveData)` → `PlayerSpawner.EnsurePlayer()` → `player.Initialize(session.Stats)` → (저장된 스테이지가 데이터에 없으면 중단) → `CombatSimulation.Initialize`(월드 + `EncounterDirector`) → `GameManager.Initialize(session, player, director)` → 배경 → `UIRoot.Initialize(session, 스킬 목록, player, director)` → `director.Begin(스테이지)`. 모든 구독이 연결된 뒤에 흐름을 시작하므로 처음 값이 반응형으로 전달된다(예전의 `OnGameStarted`가 필요 없다). 밸런스 데이터(`PlayerStatDatabase`/`PlayerUpgradeConfig`) 참조도 플레이어 프리팹이 아니라 씬의 `GameInitializer`에 있다. Awake가 아니라 Start인 이유: 다른 객체들의 Awake(이벤트 구독, 싱글턴 등록, `PlayAreaBounds` 계산)가 끝난 뒤 연결해야 하기 때문 — "Awake = 자기 자신, Start = 다른 객체와 연결".
- **저장 데이터 변환은 `GameSession.Apply(PlayerSaveData)` / `GameSession.ToSaveData()` 두 곳뿐.** `Apply`는 객체를 새로 만들지 않고 값만 덮어쓴다(세션/Stats/Wallet을 참조하는 쪽이 옛 객체를 붙잡지 않게). 세이브 로드와 이후 서버 롤백(`docs/SERVER_DESIGN.md`)이 같은 경로를 쓴다.
- **실행 중 진행도 교체는 `GameManager.LoadSnapshot(스냅샷)`** — 저장과 같은 검사 → 그 스테이지의 재시작을 디렉터에 받아 둔 뒤(받지 못하면 바꾸지 않음) JSON 복사본을 `Apply` → 다음 고정 단계에 전환, 가운데에서 입장(저장). `Apply`는 넘겨받은 객체를 그대로 붙잡으므로 직접 부를 때는 복사본을 넘긴다. 진행도는 반응형이라 `Apply`가 값만 덮어쓰면 구독하는 쪽(캐릭터 능력치·장착 스킬, HUD, 캐릭터 탭, 스킬 슬롯 바)이 따라온다(예전의 "다시 읽어라" 사건 `OnReloaded`는 4-B에서 없앴다). **진행도에서 읽은 값을 쓰는 새 코드는 상태를 구독할 것**(`.Value`를 복사해 두면 교체 뒤 옛 값이 남는다).
- **진행도 모델은 반응형이다**(`docs/COMBAT_ARCHITECTURE.md` 10-10). `PlayerStats`: 상태 `Level`, `Exp`, `UpgradeLevel(종류)`, `LearnedSkills`, `EquippedSlots`, 파생 상태 `RequiredExp`, `CanLevelUp`, `IsMaxLevel`, `CombatStats`(최대 HP, 공격력, 공격 간격), 사건 `LevelReached`, `Upgraded`, `SkillLearned`, `SkillEquipChanged`(저장 계기). `GoldWallet.Gold`도 상태다. 주인은 저장 데이터(DTO)를 먼저 다 바꾸고 상태를 알린다. 파생 상태(`DerivedProperty<T>`)는 DTO에서 계산해서 교체 도중의 조합(새 레벨 + 옛 업그레이드)이 보이지 않는다. 한 상태의 구독 콜백에서 다른 상태의 `.Value`를 읽으면 `Apply` 도중에는 아직 옛 값일 수 있다(같은 프레임 안이라 화면에는 마지막 값이 그려진다). 계산이 필요하면 파생 상태로 만든다.
- **저장 백엔드는 `ISaveStorage`로 추상화**(`Assets/Scripts/Player/ISaveStorage.cs`, 최상위 `PlayerSaveData` 기준). `LocalFileSaveStorage`가 현재 구현체(JSON을 `Application.persistentDataPath/player_save.json`에 입출력). `SaveStorageProvider.Current`가 스위치 지점 — 나중에 서버 저장으로 바꿀 때 `ServerSaveStorage : ISaveStorage`를 새로 만들고 `SaveStorageProvider.Current`에 대입하는 한 줄만 바꾸면 됨. `Load()`는 동기(`GameInitializer.Start()`에서 즉시 써야 해서), `Save()`는 `IEnumerator`(코루틴) — 호출부가 기다리지 않는 fire-and-forget이라 서버 지연을 흡수할 수 있게 미리 비동기 모양으로 잡아둔 것.
- **손상된 세이브는 새 데이터로 대신하지 않는다.** `Load()`는 파일이 없을 때만 신규 데이터를 주고, 파일이 있는데 온전히 읽을 수 없으면 예외를 던진다. `GameInitializer`가 잡아서 세션 없이 중단하므로 종료 저장을 포함한 어떤 저장도 원본을 건드리지 않는다. 새 저장소 구현체도 이 규칙을 지킬 것.
- **검사는 `SaveDataValidator`(`Assets/Scripts/Player/SaveDataValidator.cs`) 한 곳.** `JsonUtility`는 없는 필드를 기본값으로 채워 변환 후에는 손상을 알 수 없어서, 변환 전에 `JObject`로 필수 키/타입/하한/슬롯 길이/중복 키를 본다. `Save()`도 같은 검사를 통과한 값만 쓴다(`NaN` 등이면 에러 로그 후 기존 파일 유지). 상한은 보지 않는다(데이터가 줄었을 때 정상 세이브를 막지 않게).
- **`PlayerStats`는 `ISaveStorage`를 모름.** MonoBehaviour가 아니라 코루틴을 못 돌리는 순수 클래스라서, 실제 저장 트리거(코루틴 시작)는 항상 MonoBehaviour 쪽에서 함. `PlayerStats.TryLevelUp()`/`Upgrade()`/`LearnSkill()`/`EquipSkill()`/`UnequipSkill()`은 데이터와 상태만 바꾸고 사건(`LevelReached`/`Upgraded`/`SkillLearned`/`SkillEquipChanged`)을 낸다. **`GameManager`가 이 사건들을 `Initialize()`에서 직접 구독해서 `SaveGame()`을 호출**(`PlayerController`는 거치지 않음) — `PlayerController`는 `RefreshEquippedSkills`처럼 자기 런타임 상태와 진짜 관련 있는 구독만 갖고, 저장은 "언제 저장할지"를 실제로 결정하는 `GameManager`의 책임으로 분리함.
- **저장은 `GameManager.SaveGame()` 한 곳에서 `session.ToSaveData()`를 저장.** 스테이지 번호는 `GameManager`가 흐름의 `StageEntered`를 받아 세션에 반영해 둔다. 업그레이드/스킬학습/스킬장착/스테이지전환 등 저장이 일어나는 모든 경로가 이 메서드 하나로 모인다.
- **재화는 `GameNumber`(`Assets/Scripts/Economy/GameNumber.cs`, 내부 `double`).** 골드, 업그레이드 비용, 몬스터 처치 보상. 저장 DTO(`PlayerSaveData.gold`)는 `double`, 데이터 테이블(`MonsterData.goldReward`)은 `int` 그대로 — 구조체를 직렬화 대상에 넣지 않는다(직렬화 함정). 예전 세이브의 정수 `gold`는 `double` 필드로 그대로 읽힌다(확인함).
- ⚠️ **`PlayerSaveData`/`PlayerStatsSaveData`의 필드나 슬롯 수를 바꾸면 기존 세이브가 깨짐** — ScriptableObject `.asset`의 "직렬화 함정"과 같은 종류(`JsonUtility`도 필드 이름으로 매칭). 검사 대상 필드의 이름이나 슬롯 수를 바꾸면 기존 세이브가 **손상으로 차단되고**, 새로 추가한 필드는 검사되지 않아 **조용히 기본값으로 채워진다.** 새 필드를 필수로 검사하려면 버전 변환이 먼저다(`docs/TODO.md` "저장"). 구조를 바꿀 땐 `%LOCALAPPDATA%\..\LocalLow\DefaultCompany\IdleGameProtoType\player_save.json`을 직접 새 스키마로 마이그레이션할 것.
- **`OnApplicationQuit`/`OnApplicationPause(true)`는 `SaveGameImmediate()`로 동기 처리.** `StartCoroutine`으로 예약하면 앱이 곧 종료/일시정지돼서 코루틴이 실행될 프레임을 못 받을 수 있어, 이 두 경로만 `IEnumerator.MoveNext()`를 끝까지 직접 돌려서 저장이 확실히 끝나게 함. (로컬 저장 기준 안전 — 서버 구현체로 바뀌면 응답을 못 기다리고 끊길 수 있는데, 그건 그때 "종료 직전 저장을 어떻게 보장할지" product 차원에서 다시 봐야 함.)
- **자동저장 체크포인트**: 종료/일시정지, 스테이지 입장(전환 가운데, 게임 시작 제외), 레벨업(4-B, D30), 업그레이드, 스킬 학습/장착. 경험치 획득은 저장하지 않는다(처치마다 저장하면 잦다. 위 계기에서 같이 저장된다). 주기적(타이머) 자동저장은 의도적으로 안 둠.
- **스테이지 중간 진행도(인카운터 진행 수)는 저장 안 함** — 의도된 범위 제한. 저장된 스테이지 번호로 복귀하면 그 스테이지를 처음부터(인카운터 1부터) 다시 시작한다. 전투 중 HP/몬스터 상태까지 직렬화하려면 전투원/스포너 상태까지 손대야 해서 범위 밖으로 뺌.
- 세이브 파일 경로: `C:\Users\<user>\AppData\LocalLow\DefaultCompany\IdleGameProtoType\player_save.json`
- **에디터 검증용 세이브 프로필**(`SaveProfile`, `Assets/Scripts/Player/SaveProfile.cs`): 에디터에서만 켤 수 있고, 켜면 세이브 폴더가 `persistentDataPath/profiles/<이름>/`로 바뀐다(형식과 저장 코드는 같음). 값은 `SessionState`라 에디터를 끄면 기본으로 돌아오고, 플레이 중에는 바꿀 수 없다. 메뉴는 `IdleGame/Save Profile/...`, `IdleGame/Reset Save Data`는 지금 켜진 프로필의 세이브를 지운다. 빌드에는 프로필 코드가 없어서 항상 기본 경로를 쓴다.

## 개발자 도구 (`docs/DEV_TOOLS.md`)

- **에디터와 개발 빌드에만 있다.** 어셈블리 `IdleGameProtoType.DevTools`(`Assets/Scripts/DevTools/`)의 define constraint가 `UNITY_EDITOR || DEVELOPMENT_BUILD`라 릴리스 빌드에서는 컴파일되지 않는다. 게임 코드는 도구를 모른다(도구가 `GameInitializer.Initialized`에 붙는다). **게임 어셈블리에서 DevTools를 참조하지 말 것**(릴리스 빌드가 깨진다).
- 화면 오른쪽 위(골드 아래)의 `DEV` 버튼 또는 에디터의 `` ` `` 키로 연다. 패널은 IMGUI다(에셋을 가질 수 없어서 "새 팝업은 UIWindow" 규칙의 예외). 열려 있는 동안 게임 UI 입력은 `InputBlocker`로 막는다.
- 명령: 스테이지 이동·처음부터·보스 도전(버튼과 같은 명령이라 도전 가능할 때만, D27), 골드 x10·경험치 +1레벨분, 레벨 지정, 백업·복원·새로 시작, 배속 x1~x10, 판정 몸 표시. 스테이지 명령은 흐름(`EncounterDirector`)의 명령을 그대로 부른다. 코드에서는 `DevTools.Commands`(Claude가 플레이 중 `script eval`로 쓸 수 있다).
- **개발 도구 전용 게임 API를 만들지 않는다.** 더하는 명령은 보상 경로(`Wallet.AddGold`, `PlayerStats.AddExp`), 통째로 바꾸는 명령은 `GameManager.LoadSnapshot` 하나를 지난다. 덮어쓰기 전에는 `<세이브 폴더>/dev_backups/`에 자동 백업하고, 백업이 실패하면 덮어쓰지 않는다.

## 작업 시 주의사항

- **테스트는 작성해도 된다**(2026-11-23 합의 변경. 전에는 새 테스트를 만들지 않기로 했다). 반복해서 쓸 검증은 테스트로 `Assets/Tests/EditMode/`에 둔다. 지금 수치에서 통과하는지가 아니라 불변식과 극단값을 확인한다(예: `docs/COMBAT_ARCHITECTURE.md` 5절). 일회성 측정 스크립트는 저장소에 넣지 않고 쓰고 지운다. 실패 중인 기존 테스트 2개(`AddExp`)는 고칠지 지울지 결정 대기(`docs/TODO.md`).
- **`git commit`은 사용자가 그 턴에 명시적으로 지시했을 때만.**
- 일부 `.cs` 파일은 CP949(EUC-KR) 인코딩 — UTF-8로 Edit/Write하면 한글 주석이 깨질 수 있으니 주의. (직접 새로 작성하는 파일은 UTF-8로 써도 무방, 기존 CP949 파일을 수정할 때만 주의)
- IData 구현 클래스의 필드/프로퍼티를 리네임하는 작업은 반드시 위 "직렬화 함정" 섹션을 먼저 확인하고, `.asset` 데이터와의 정합성을 같이 맞출 것.

## 외부 도구: Unity 에디터 조작

에디터 조작은 **일의 종류로 나눈다**(2026-11-25 정함). **사용자에게 에디터를 닫아 달라고 하지 않는다.** 기계별 경로(에디터 설치 위치, 사본 위치)는 이 파일이 아니라 Claude memory에 있다.

| 일 | 어디서 |
|---|---|
| 코드를 고친 뒤 컴파일 확인, C# 실행, 콘솔 보기, 씬·에셋 편집 | 사용자가 켜 둔 에디터(1). 꺼져 있으면 사본 에디터(2)에서 컴파일하고, 씬·에셋 편집은 원본 배치 모드(3) |
| 테스트, 플레이 시나리오, 긴 측정, Android 빌드 | 검증용 사본 프로젝트의 Claude 전용 에디터(2). 시작 전 `Tools/VerifyCopy.ps1 -Action Sync` |
| 화면을 봐야 하는 확인 | Android 기기(4). 에디터 화면이 꼭 필요하면 사용자 에디터에서 플레이(1의 세이브 보호 규칙) |

이유: 켜 둔 에디터는 빠르지만(명령 0.2~0.6초, 컴파일 수 초) 사용자의 것이다. 플레이 모드·테스트·빌드는 사용자 작업을 끊고, 테스트의 무한 반복은 사용자 에디터를 멈춘다. 예전에는 그런 일마다 사용자가 에디터를 닫고 원본에서 배치 모드를 돌렸다(실행마다 시작 1~2분, 화면 없음). 사본은 프로젝트 폴더가 달라 잠금이 겹치지 않고, 상주 에디터라 시작 비용이 한 번뿐이다. 다른 방법과 비교해 정했다(2026-11-25): 링크 사본(ParrelSync처럼 `Assets`를 공유)은 사본의 쓰기(임시 검증 스크립트, 플레이 중 바뀌는 에셋)가 원본에 들어가고, git worktree는 커밋 전 코드를 담지 못한다(커밋은 지시가 있을 때만).

1. **실행 중인 에디터에 연결: Unity CLI + Pipeline 패키지** — 사용자가 에디터를 켜 두었고 프로젝트에 `com.unity.pipeline`이 있으면 `unity status`로 연결을 확인하고 `unity command ...`(플레이 모드, C# 실행, 콘솔, 스크린샷)로 실시간 조작한다. **셸을 쓸 수 있으면 MCP보다 CLI를 우선한다**(CLI가 더 많은 기능을 쓴다). 사용법은 `unity-cli` 스킬. **현재 상태: `com.unity.pipeline` 1.0.1 설치됨, 회사 PC에서 켜 둔 에디터 연결 확인**(`unity status`, `script eval`, `console`, `capture editor_window`, `recompile` 동작. Claude 데스크톱 앱의 MSIX 샌드박스에서도 됨). 사본 에디터가 함께 떠 있을 수 있으므로 **모든 명령에 `--project-path <원본>`을 붙인다.**
   - **사용자 세이브 보호(필수):** 켜 둔 에디터는 사용자의 진짜 세이브를 쓴다. 플레이 모드에 들어가기 전에 항상 `SaveProfile.SetActive(SaveProfile.VerificationProfile)`(`claude` 프로필)을 켜고, 진입 후 콘솔에 `[Save] 검증용 프로필 'claude' 사용 중` 경고가 있는지 확인한 뒤 검증한다. 경고가 없으면 즉시 플레이를 끝낸다. 검증이 끝나면 플레이를 나와서 `SaveProfile.SetActive(null)`로 되돌린다.
   - **열린 씬이 MainScene이 아닐 수 있다**(빈 새 씬이 열려 있던 적 있음). 사용자가 연 씬을 바꾸지 말고 `EditorSceneManager.playModeStartScene`에 MainScene을 넣고 플레이한 뒤 `null`로 되돌린다.
   - **자동 반복 측정:** 플레이를 여러 번 돌릴 때는 임시 `[InitializeOnLoad]` 러너가 `SessionState`의 실행 목록을 하나씩 꺼내 플레이하고, 런타임 쪽 임시 스크립트(`#if UNITY_EDITOR`, `RuntimeInitializeOnLoadMethod`)가 `Time.captureDeltaTime`으로 프레임을 고정해 결과를 쓰고 플레이를 끝낸다. 결과와 큐 파일은 `Library/_verify/`처럼 `Temp/` 밖에 둔다(에디터를 닫으면 `Temp/`가 통째로 지워진다). 배치 모드에서는 러너를 `-executeMethod`로 시작하고 큐가 끝나면 `EditorApplication.Exit`로 종료한다(2·3단계 검증에 씀). 전투 고정 타임스텝 검증에 쓴 것이 이 방식이다(스크립트는 지웠다). 플레이 중에는 스크립트를 고치지 않는다(재컴파일이 측정 사이에 끼어든다).
   - **테스트는 에디터의 메인 스레드에서 돈다.** 새 테스트나 테스트하는 코드에 무한 반복이 있으면 그 에디터 전체가 멈추고, 프로세스를 끝내는 것 말고는 풀 수 없다(2026-11-24에 `ActionRunner` 버그로 한 번 겪었다). 그래서 테스트는 사용자 에디터가 아니라 사본 에디터(2)에서 돌린다. 사용자 에디터를 강제 종료할 때는 먼저 사용자 승인을 받는다.
   - **플레이/빌드 후 정리:** TMP 동적 폰트(`Assets/Fonts/MalgunGothic SDF.asset`)가 플레이 중 채운 글자로 바뀌고, Android 빌드 후 `ProjectSettings/UnityConnectSettings.asset`의 `m_Enabled`가 1이 된다. 플레이하면 `persistentDataPath/Unity/`(분석 캐시)도 생긴다. 셋 다 되돌리거나 지운다.
   - **빌드는 사본(2)에서 한다.** 켜 둔 에디터에서도 `unity command build run`으로 되지만 빌드하는 몇 분 동안 사용자 에디터가 묶인다.
2. **검증용 사본 프로젝트 + Claude 전용 상주 에디터** — 테스트, 플레이 시나리오, 긴 측정, 빌드. 개발자 도구를 이 방식으로 검증했다(2026-11-25).
   - **위치:** 프로젝트 옆 폴더(회사 PC는 `..\IdleGameProtoType_verify`). git 저장소가 아니라 원본의 복사본이다.
   - **동기화는 `Tools/VerifyCopy.ps1` 하나로만 한다**(원본 → 사본 한 방향). `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/VerifyCopy.ps1 -Action <Sync|Check|Clean>`. 사본 경로는 원본 옆 `<원본 폴더 이름>_verify`로 계산한다.
     - `Sync`: `Assets`, `Packages`, `ProjectSettings`를 미러 복사 → 사본 에디터가 떠 있으면 새로 읽고 컴파일 → 원본과 파일 내용(SHA-256) 비교. **종료 코드 0일 때만 검증을 시작한다**(1 다름, 2 실행 실패, 3 사본 컴파일 실패). 약 40초(에디터 새로 읽기 포함).
     - `Check`: 비교만(약 2초). **검증 결과를 보고하기 전에** 돌려 검증하는 동안 원본이 바뀌지 않았는지 보고, 보고에 출력의 지문(`사본 = 원본 (파일 N개, 지문 …)`)을 적는다. 다르면 무엇이 다른지 보고 판단한다(사본에서 플레이하면 TMP 동적 폰트 에셋이 바뀔 수 있다).
     - 사본에서 고친 것은 다음 동기화 때 사라진다. **씬·에셋·코드 수정은 원본에서만 한다.**
     - **임시 검증 스크립트(시나리오, 러너)는 사본의 `Assets/_ClaudeVerify/`에 둔다**(에디터 스크립트는 그 안의 `Editor/`). 이 폴더와 `.meta`는 복사·삭제·비교에서 빠져 동기화해도 남는다. 원본에는 넣지 않고, 다 쓰면 지운다.
   - **만들 때:** 원본 `Library`를 복사하면 가져오기를 건너뛴다. `Bee`(Android 빌드 캐시, 약 6.7GB), `Pipeline`, `_verify`, **`ilpp.pid`, `*-lock`은 빼고** 복사한다. 2026-11-25에 `ilpp.pid`까지 복사했더니 사본 에디터가 시작하면서 사용자 에디터의 IL 후처리 프로세스를 "남은 프로세스"로 보고 종료시켰다(사용자 에디터는 다음 컴파일 때 다시 띄우므로 문제는 없었다). 만든 뒤 `Sync`가 0을 내는지 확인한다.
   - **에디터:** `Unity.exe -batchmode -projectPath <사본> -logFile <사본>\Logs\resident_editor.log`(`-quit` 없음 → 상주)를 백그라운드로 띄우고 `unity status --until-ready --project-path <사본>`. 이후 **모든 명령에 `--project-path <사본>`**.
     - 컴파일 `unity recompile`, 테스트 `unity command test run --mode editor`, 플레이 `unity command editor.playmode start|stop`, C# `unity command script eval`.
     - 빌드 `unity command build run --target Android --output-path Builds/<이름>.apk --options '["Development"]' --confirm true` 후 `unity command build status`. 사본에는 `Bee` 캐시가 없어 첫 빌드는 처음부터 한다.
     - 창 없는 에디터는 파일 변경을 스스로 보지 않는다. `Sync`가 새로 읽기와 컴파일까지 하므로, `_ClaudeVerify`의 파일을 고친 뒤에도 `Sync`를 다시 돌린다.
   - **세이브:** `persistentDataPath`가 원본과 같다(회사·제품 이름이 같다). 사본에서도 플레이 전에 `claude` 프로필을 켜고 경고를 확인한다(1의 세이브 보호 규칙 그대로).
   - **수명과 크기:** 사본 에디터(메모리 약 0.9GB)는 Claude의 것이라 사용자 승인 없이 끌 수 있다. 검증이 이어지는 동안은 켜 두고, 그날 검증이 끝나거나 한동안 검증할 일이 없으면 끄고(`unity close <사본>`) `Clean`을 돌린다. `Clean`은 빌드 캐시(`Library/Bee`, Android 빌드 뒤 약 6GB)와 가장 최근 APK가 아닌 빌드 결과(기호 폴더 약 0.6GB 포함)를 지운다. 사본은 빌드 직후 약 9GB, `Clean` 뒤 약 2GB. 대가는 다음 빌드가 처음부터(약 8분)인 것과 다음 시작 때 스크립트 전체 재컴파일이다. 사본 폴더째 지워도 된다(다시 만들면 된다).
3. **원본 배치 모드(사용자 에디터가 꺼져 있을 때만)** — `Unity.exe`를 창 없이 띄워 일을 시키고 종료한다. 씬·에셋 편집처럼 원본에서 해야 하는 일에 쓴다. 0단계(GameSession/GameInitializer)를 이 방식으로 구현·검증했다.
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
4. **Android 실기기 (adb)** — 최종 확인은 Android 빌드를 실기기에 설치해서 한다. 개발 빌드(`Development`)에는 개발자 도구가 들어 있다(아래 "개발자 도구").
   - **adb는 PC 전체에서 한 버전만 쓴다.** adb 서버는 PC에 하나(포트 5037)이고, 버전이 다른 adb가 명령을 보내면 서버를 끄고 자기 버전으로 다시 띄운다. 그래서 다른 프로젝트(liveeye_kr 웹 개발)와 같은 PC에서 쓰면 서로의 서버를 꺼 버린다. 기준은 **Unity에 딸린 adb**이고, 다른 도구가 쓰는 SDK의 `platform-tools`도 같은 것으로 맞춰 둔다(회사 PC: `~/android-dev/sdk/platform-tools`를 Unity 것으로 교체, 원본은 `platform-tools-37.0.1-backup`). **Unity를 업그레이드하면 adb 버전이 바뀌므로 그쪽 `platform-tools`도 다시 맞춘다.**
   - 설정은 PC별 `.claude/settings.local.json`(원본: claude-sync `setup/`)의 `env`에 있다: `ADB`(Unity의 adb 경로), `ANDROID_SERIAL`(이 프로젝트 테스트 기기). `ANDROID_SERIAL`이 있으면 adb가 `-s` 없이도 그 기기에만 명령을 보낸다. 같은 PC에 다른 프로젝트가 쓰는 기기가 함께 연결돼 있을 수 있으므로 다른 기기에는 설치·실행·입력을 하지 않는다.
   - `adb kill-server`/`start-server`는 권한 규칙으로 막혀 있다(두 프로젝트 모두). 서버를 다시 띄우는 판단은 사용자가 한다.
   - 빌드: 사본 에디터(2)의 `build run`, 또는 원본 배치 모드 `-buildTarget Android`로 APK만 만들고(Build & Run을 쓰지 않음), 설치는 `"$ADB" install -r <apk>`. 확인은 `screencap`, `logcat -s Unity:*`, `shell input`. **배치 모드 에디터는 종료할 때 출력 폴더 `Temp/AndroidBuild/`를 지운다**(2026-11-24 확인). 그래서 출력은 git이 무시하는 `Builds/`(예: `Builds/idleproto.apk`)에 둔다. `Library/` 안으로는 Unity가 빌드를 거부한다. 빌드 캐시의 `Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build/outputs/apk/release/launcher-release.apk`에서 설치해도 된다. 배치 빌드에는 임시 `-executeMethod` 스크립트(`BuildPipeline.BuildPlayer`)를 쓰고 끝나면 지운다.
5. **Unity MCP(`mcp__UnityMCP__*`)** — 과거 세션에서 쓰던 방식. 세션에 연결돼 있을 때만 쓸 수 있다. `execute_code`는 종종 mono.exe 경로 길이 문제로 환경적 오류가 나는데, 코드 내용과 무관한 툴 자체 이슈인 경우가 있음 — 의심되면 `return 1+1;` 같은 최소 코드로 먼저 격리 확인.
