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

    protected override Team Team => Team.Ally;

    public PlayerStats Stats => stats;
    public CharacterData Character => character;
    public float AttackInterval => stats != null ? stats.AttackInterval : 0f;
    public GameNumber CurrentHp => Combatant.Hp;
    public GameNumber MaxHp => Combatant.MaxHp;

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
    public void Initialize(PlayerStats stats, CharacterData character)
    {
        Unsubscribe();

        this.stats = stats;
        this.character = character;

        loadout.SetSlots(stats.EquippedSlots.Value);
        Combatant.Spawn(BuildStats(), BodyCenterX(transform.position.x), hitboxProfile.HalfWidth, timelines, loadout);

        stats.OnSkillEquipChanged += RefreshEquippedSkills;
        stats.OnUpgraded += RefreshStats;
        stats.OnLevelUp += HandleLevelUp;
        stats.OnReloaded += HandleReloaded;
#if UNITY_EDITOR
        DataTuning.Changed += RefreshStats;
#endif
    }

    private void OnDestroy() => Unsubscribe();

    private void Unsubscribe()
    {
        if (stats != null)
        {
            stats.OnSkillEquipChanged -= RefreshEquippedSkills;
            stats.OnUpgraded -= RefreshStats;
            stats.OnLevelUp -= HandleLevelUp;
            stats.OnReloaded -= HandleReloaded;
        }
#if UNITY_EDITOR
        DataTuning.Changed -= RefreshStats;
#endif
    }

    private CombatantStats BuildStats() => new CombatantStats
    {
        MaxHp = stats.MaxHp,
        AttackDamage = stats.AttackDamage,
        AttackInterval = stats.AttackInterval,
        AttackRange = character != null ? character.AttackRange : 0f,
        MoveSpeed = character != null ? character.MoveSpeed : 0f,
        MovePolicy = MovePolicy.Advance,
    };

    // 업그레이드·레벨업·튜닝으로 능력치가 바뀌면 다시 넣는다(현재 HP는 유지).
    private void RefreshStats()
    {
        if (stats != null) Combatant.UpdateStats(BuildStats());
    }

    private void HandleLevelUp(int level) => RefreshStats();

    // 진행도가 통째로 바뀌면(GameManager.LoadSnapshot) 진행도에서 온 것을 모두 다시 넣는다.
    // 스킬 슬롯 바도 이 사건에서 캐릭터의 장착 스킬을 읽으므로, 캐릭터가 먼저 구독돼 있어야 한다(Initialize가 UI보다 먼저).
    private void HandleReloaded()
    {
        RefreshStats();
        RefreshEquippedSkills();
    }

    // ── 연출 ──

    public override void HandleLocomotion(Combatant self, bool moving)
    {
        if (view != null) view.PlayLocomotion(moving);
    }

    // 사망 동작이 끝나면 재시작을 알린다(흐름은 4단계에서 EncounterDirector로).
    public override void HandleDeathCompleted(Combatant self) => GlobalGameEvents.TriggerPlayerDied();

    // ── 흐름(GameManager)이 부른다 ──

    public void Revive() => Combatant.Revive();
    public void FullHeal() => Combatant.Heal(Combatant.MaxHp);
    public void AddExp(int amount) => stats.AddExp(amount);
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
