# 2D 방치형 게임

데이터 기반 설계로 만든 2D 방치형 액션 RPG 프로토타입입니다.

- 엔진: Unity 6000.4.9f1 (Unity 6)
- 개발 기간: 2026.06 ~ 진행 중

## 목차

- [스크린샷](#스크린샷)
- [핵심 설계](#핵심설계)
- [핵심 게임플레이](#핵심-게임플레이)
- [아키텍처](#아키텍처)
- [프로젝트 구조](#프로젝트-구조)
- [실행 방법](#실행-방법)
- [다음 단계](#다음-단계)

## 스크린샷

![gameplay screenshot](Assets/Screenshots/screenshot-20260616-183806.png)

> 위 이미지는 에디터 Scene 뷰 캡처입니다. 추후 실제 플레이 화면으로 교체할 예정입니다.

## 핵심 설계

1. **엑셀 → JSON → ScriptableObject 데이터 파이프라인을 설계 / 변환 툴 구현**: 기획자가 엑셀만 수정해도 게임 데이터가 자동 반영되고, 프리팹/텍스처 등 외부 참조는 이름 기반으로 자동 매칭하며, 수동으로 연결한 아이콘/VFX 참조는 재동기화해도 보존됩니다.
2. **인터페이스 기반 확장 구조**: 데이터 타입(`IData`)과 동기화기(`IDataSyncer`)는 리플렉션으로 자동 탐색되어 새 데이터 테이블을 추가해도 변환기 코드를 건드리지 않고, 스탯(`IUpgradeStat`)·스킬 효과(`ISkillBehavior`)도 구현 클래스 하나만 추가하면 확장됩니다.
3. **이벤트·인터페이스로 일관되게 낮춘 결합도**: 전투 보상, UI 갱신, 스테이지·배경 전환까지 — 한 시스템은 "무슨 일이 일어났는지"만 흘려보내고 처리는 각 구독자가 맡는 구조를 프로젝트 전반에 동일하게 적용했습니다. 구체 타입 의존이 필요한 곳도 `IDamageable` / `IHasHp` 같은 인터페이스로 추상화해 플레이어와 몬스터를 같은 코드로 다룹니다.
4. **하나의 FSM 코어로 전체 엔티티 행동을 제어**: `IState` / `StateMachine`라는 작은 계약 하나로 플레이어·몬스터·보스의 이동·전투·사망을 전부 처리합니다. 엔티티마다 독립된 `StateMachine` 인스턴스를 가지며, 보스는 `BossRunState : MonsterRunState`처럼 기존 상태를 상속해 이동 로직은 재사용하고 고유 행동만 추가합니다.

## 핵심 게임플레이

- 캐릭터가 앞으로 계속 전진 -> 몬스터를 만나면 전투를 무한 반복
- 몬스터 무리를 일정 횟수 클리어하면 보스 도전 버튼이 열리고, 보스를 처치하면 다음 스테이지로 전환
- 레벨업 및 체력 / 공격력 / 공격속도 등 골드를 소모하여 스탯 업그레이드
- 스킬 학습 → 슬롯 배치 → 쿨다운 기반 자동 사용 (피해 / 광역피해 / 회복 타입)
- 골드·상자 드랍, 데미지·힐 텍스트, 히트 파티클 등 전투 피드백

## 아키텍처

### 1. 데이터 파이프라인: Excel → JSON → ScriptableObject

```
기획자가 작성한 .xlsx
        │  ExcelToJsonConverter (Editor 전용 윈도우)
        │  - IData 구현 클래스를 리플렉션으로 자동 탐색
        │  - 엑셀 헤더 ↔ 클래스 필드를 매칭, 불일치 시 변환 자체를 중단
        ▼
Resources/Data/*.json
        │  IDataSyncer 구현체를 리플렉션으로 자동 탐색해 타입별로 디스패치
        │  (Monster / Skill / Stage / PlayerStat 전용 Syncer)
        ▼
ScriptableObject 데이터베이스 (Assets/Data/**/*.asset)
        │  프리팹·텍스처는 이름 기반 자동 매칭
        │  아이콘·VFX·SFX 등 수동 연결 참조는 재동기화해도 보존
        ▼
GameDatabaseManager → 런타임 게임 로직
```

새로운 데이터 테이블을 추가해도 변환기 본체 코드는 수정하지 않고, `IDataSyncer` 구현 클래스 하나만 추가하면 자동으로 인식됩니다.
([ExcelToJsonConverter.cs](Assets/Editor/DataImporter/ExcelToJsonConverter.cs), [IDataSyncer.cs](Assets/Editor/DataImporter/IDataSyncer.cs))

### 2. 전투 FSM (State Pattern + Template Method)

`IState`(Enter / Execute / Exit 메서드)와 `StateMachine`만으로 플레이어·일반 몬스터·보스의 이동·전투·사망을 전부 같은 방식으로 제어합니다. 엔티티마다 자신만의 `StateMachine` 인스턴스를 가져서, 화면에 몬스터가 여러 마리 있어도 서로 간섭 없이 독립적으로 동작합니다. 전이 방식도 상황에 맞게 나누는데, 스크롤 on/off 같은 전역 흐름 변화는 `GlobalGameEvents` 구독으로 반응하고(Idle ↔ Run), 사거리 진입처럼 매 프레임 바뀌는 조건은 `Execute()`에서 직접 검사합니다(Run → Attack). 보스는 `BossRunState : MonsterRunState`로 기존 이동 로직을 상속해 재사용하면서 공격 사거리 판정만 추가하고, 전투 상태는 `PlayerCombatState` / `BossCombatState` 추상 클래스가 쿨타임 계산과 타격 처리를 메서드로 공유합니다.

([StateMachine.cs](Assets/Scripts/FSM/StateMachine.cs), [PlayerCombatState.cs](Assets/Scripts/FSM/Player/PlayerCombatState.cs), [BossCombatState.cs](Assets/Scripts/FSM/Monster/BossCombatState.cs))

### 3. 스킬 시스템 (Strategy Pattern)

스킬의 실제 동작(`ISkillBehavior`)은 코드가 아니라 데이터(`SkillData.effectType`)로 결정되므로, 같은 효과 타입의 새 스킬을 추가할 때는 수치(`value1` / `value2`), (EffectFX / SoundFX)등만 다른 데이터로 추가하고 코드는 건드리지 않습니다. `Skill` 클래스가 쿨다운 계산과 실행을 함께 감싸서, FSM(PlayerSkillState)는 스킬이 피해 · 광역 · 회복 중 무엇인지 몰라도 `IsReady` / `Use()` 만으로 동일하게 다룹니다.

([Skill.cs](Assets/Scripts/Skill/Skill.cs), [ISkillBehavior.cs](Assets/Scripts/Skill/ISkillBehavior.cs))

### 4. 이벤트 기반 결합도 분리

`GlobalGameEvents`(스테이지 전환, 스크롤 상태 등 흐름)와 `GlobalCombatEvents`(피격, 사망, 회복 등 전투)로 정적 이벤트를 분리했습니다. 몬스터(`Monster.Die()`)는 죽었다는 사실과 보상치만 이벤트로 흘려보내고, 실제 골드/경험치 지급은 `GameManager`가, 드랍 이펙트는 `CombatEffectManager`가 각자 구독해서 처리합니다 — 몬스터 클래스는 보상 시스템의 존재 자체를 모릅니다.

([GlobalCombatEvents.cs](Assets/Scripts/Event/GlobalCombatEvents.cs), [GameManager.cs](Assets/Scripts/Manager/GameManager.cs))

### 5. 오브젝트 풀링

몬스터, 데미지 텍스트, 히트 파티클, 보상 드랍 이펙트를 모두 `PoolManager`가 Unity 내장 `ObjectPool<GameObject>`로 관리합니다. 스테이지 전환 시 해당 프리팹의 풀만 선택적으로 비우는 방식으로 메모리를 관리합니다.

([PoolManager.cs](Assets/Scripts/Manager/PoolManager.cs))

## 프로젝트 구조

```
Assets/
├─ Scripts/
│  ├─ Data/         # IData 구현체 + ScriptableObject 데이터베이스 정의
│  ├─ Manager/       # GameManager, StageManager, PoolManager, UIManager 등 매니저
│  ├─ FSM/           # 상태머신 + Player/Monster 상태 클래스
│  ├─ Skill/         # 전략 패턴 기반 스킬 효과
│  ├─ Player/        # 스탯 / 저장데이터 / 업그레이드
│  ├─ UI/            # UIBase 계층 + 화면별 컴포넌트
│  └─ Event/         # 전역 이벤트 버스
├─ Editor/DataImporter/  # Excel → JSON → ScriptableObject 변환 툴
├─ Data/                 # 데이터베이스 ScriptableObject 에셋
└─ Resources/             # 런타임 로드 프리팹 / JSON
```

