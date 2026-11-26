using UnityEngine;

// 플레이어 캐릭터의 호스트(docs/COMBAT_ARCHITECTURE.md 8-3). 전투 규칙은 Combatant(순수 C#)가 갖고,
// 이 클래스는 데이터를 능력치로 바꿔 넣고, 그리기와 UI 입구를 맡는다.
//
// 능력치의 출처:
//   - 레벨·업그레이드에 따라 변하는 것(HP, 공격력, 공격 간격): PlayerStats(진행도, GameSession 소유)
//   - 캐릭터마다 다른 것(사거리, 이동 속도): 엑셀 캐릭터 표(CharacterData, D12)
// GameInitializer가 Initialize(stats, character)로 넘겨준다.
public class PlayerController : CombatantHost
{
    [Tooltip("스프라이트 피벗이 발 기준이 아닐 때, 발이 바닥(PlayAreaBounds.GroundY)에 닿아 보이도록 " +
             "보정하는 값(월드 유닛). MonsterEntry.groundOffset과 동일한 역할 — 캐릭터(직업)별로 다른 " +
             "스프라이트를 쓸 수 있으므로 PlayAreaBounds 쪽 전역값이 아니라 여기서 캐릭터별로 보정한다.")]
    [SerializeField] private float groundOffset = 0f;
    public float GroundOffset => groundOffset;

    private PlayerStats stats;
    private CharacterData character;
    private SkillLoadout loadout;
    private CompositeDisposable subscriptions = new CompositeDisposable();

    protected override Team Team => Team.Ally;

    public PlayerStats Stats => stats;
    public CharacterData Character => character;

    // 시뮬레이션 시계. 월드에 등록되기 전에는 null(UI 쿨다운 표시는 시각 0 기준).
    public SimClock Clock => Combatant.World?.Clock;

    protected override void Awake()
    {
        base.Awake();
        loadout = new SkillLoadout(PlayerStatsSaveData.SkillSlotCount, id => GameDatabaseManager.Instance?.GetSkill(id));

        // 사망 시간표가 없으면 사망 즉시 재시작된다(진행은 멈추지 않는다). 숨기지 않고 알린다.
        if (timelines != null && !timelines.TryGet(ActionKeys.Death, out _))
            Debug.LogError($"[PlayerController] {name}의 행동 시간표에 사망(Death)이 없습니다. 사망 동작을 기다리지 않고 재시작합니다.");
    }

    // 다른 객체(진행도, 데이터)에 의존하는 초기화는 Awake가 아니라 GameInitializer가 부르는 여기서 한다.
    // 진행도의 상태를 구독하므로 레벨업, 업그레이드, 진행도 교체(Apply)가 모두 같은 길로 들어온다.
    public void Initialize(PlayerStats stats, CharacterData character)
    {
        subscriptions.Dispose();
        subscriptions = new CompositeDisposable();

        this.stats = stats;
        this.character = character;

        loadout.SetSlots(stats.EquippedSlots.Value);
        Combatant.Spawn(BuildStats(stats.CombatStats.Value), BodyCenterX(transform.position.x), hitboxProfile.HalfWidth, timelines, loadout);

        // 능력치가 바뀌면 다시 넣는다(현재 HP는 유지). 구독하는 순간 한 번 오지만 값이 같아 그대로다.
        stats.CombatStats.Subscribe(s => Combatant.UpdateStats(BuildStats(s))).AddTo(subscriptions);
        // 장착이 바뀌면 다시 맞춘다. 스킬 슬롯 바는 캐릭터의 장착 스킬을 읽으므로 캐릭터가 먼저 구독한다(Initialize가 UI보다 먼저,
        // 같은 상태의 구독자는 구독한 순서로 알림을 받는다).
        stats.EquippedSlots.Subscribe(_ => RefreshEquippedSkills()).AddTo(subscriptions);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        subscriptions.Dispose();
    }

    private CombatantStats BuildStats(PlayerCombatStats s) => CombatantSetup.ForPlayer(s, character);

    // ── 연출 ──

    protected override void HandleLocomotion(bool moving)
    {
        if (view != null) view.PlayLocomotion(moving);
    }

    // 사망 동작이 끝나도 할 일이 없다: 흐름(EncounterDirector)이 다음 단계 ①에서 아군 전멸을 월드에 묻는다.
    // 부활·회복도 흐름이 전투원(Combatant)에 직접 한다. 처치 보상은 사망 사건을 받은 GameManager가 진행도에 직접 준다.

    public void SaveProgress() => GameManager.Instance?.SaveGame();

    // ── 스킬(UI 입구) ──

    // 슬롯 배치가 바뀌면 다시 맞춘다. 쿨다운은 스킬 id에 붙어 있어서 슬롯을 옮겨도 유지된다(SkillLoadout).
    public void RefreshEquippedSkills()
    {
        loadout.SetSlots(stats.EquippedSlots.Value);
        Combatant.Combat.ClearRequest();
    }

    public Skill GetEquippedSkill(int slotIndex) => loadout.GetSlot(slotIndex);

    public float GetSkillCooldownRatio(int slotIndex)
    {
        Skill skill = loadout.GetSlot(slotIndex);
        return skill != null ? skill.CooldownRatio(Now) : 0f;
    }

    // UI(스킬 슬롯 클릭)가 호출한다. 요청만 남기고, 사거리 안에 대상이 있을 때 다음 결정 시점에 쓴다
    // (달리는 중에 누르면 교전이 시작될 때 쓴다). 위치는 고정 단계 안에서만 읽는다(docs/COMBAT_DESIGN.md 3-2).
    public void RequestSkillSlot(int slotIndex)
    {
        if (!IsAlive) return;

        Skill skill = loadout.GetSlot(slotIndex);
        if (skill == null || !skill.IsReadyAt(Now)) return;

        Combatant.Combat.RequestSkill(skill);
    }

    private double Now => Clock != null ? Clock.Now : 0.0;
}
