using System;

// 플레이어 전투원과 진행도의 연결(docs/COMBAT_ARCHITECTURE.md 12절 D45). 순수 C#.
// - 능력치의 출처: 레벨·업그레이드로 변하는 것(HP, 공격력, 공격 간격)은 진행도(PlayerStats), 캐릭터마다 다른 것(사거리, 이동 속도)은
//   캐릭터 표(CharacterData, D12). 진행도의 상태를 구독하므로 레벨업, 업그레이드, 진행도 교체(Apply)가 모두 같은 길로 들어온다.
// - UI의 입구: 스킬 슬롯 요청, 쿨다운 비율, 장착 스킬, HP 상태. UI는 뷰(GameObject)가 아니라 이것을 본다.
// 예전에는 이 일을 플레이어의 뷰(PlayerController)가 했다(12-1 F2).
public sealed class PlayerCombat : IDisposable
{
    private readonly CompositeDisposable subscriptions = new CompositeDisposable();
    private readonly ReactiveProperty<HpState> hp = new ReactiveProperty<HpState>();
    private readonly PlayerStats stats;
    private readonly SkillLoadout loadout;

    // 플레이어의 처음 위치(몸 중심). 월드는 인카운터마다 원점을 앞장 근처로 옮기고(D16) 카메라가 따라가므로 값 자체는 뜻이 없다.
    // 예전에는 씬의 시작 지점(그림의 피벗)이었다(12절 D50).
    public const float StartX = 0f;

    public PlayerCombat(PlayerStats stats, CharacterData character, in CombatantBody body, Func<int, SkillData> findSkill)
    {
        this.stats = stats ?? throw new ArgumentNullException(nameof(stats));
        Character = character ?? throw new ArgumentNullException(nameof(character));

        loadout = new SkillLoadout(PlayerStatsSaveData.SkillSlotCount, findSkill);
        loadout.SetSlots(stats.EquippedSlots.Value);
        Combatant.Spawn(CombatantSetup.ForPlayer(stats.CombatStats.Value, character), StartX, body.HalfWidth,
            body.Timelines, loadout, default, CombatantArchetype.Character(character.Id));

        // 능력치가 바뀌면 다시 넣는다(현재 HP는 유지). 구독하는 순간 한 번 오지만 값이 같아 그대로다.
        stats.CombatStats.Subscribe(s => Combatant.UpdateStats(CombatantSetup.ForPlayer(s, Character))).AddTo(subscriptions);
        // 장착이 바뀌면 다시 맞춘다. 스킬 탭은 이 전투원의 장착 스킬을 읽으므로 여기가 UI보다 먼저 구독한다
        // (같은 상태의 구독자는 구독한 순서로 알림을 받는다. Game이 UI보다 먼저 만들어진다).
        stats.EquippedSlots.Subscribe(_ => RefreshEquippedSkills()).AddTo(subscriptions);
    }

    public Combatant Combatant { get; } = new Combatant(Team.Ally);
    public CharacterData Character { get; }

    // HP(현재, 최대). HUD가 구독한다. 월드의 사건으로 따라간다(배달 때라 같은 단계의 피해가 여러 번이어도 마지막 값).
    public IReadOnlyReactiveProperty<HpState> Hp => hp;

    // Game이 월드를 만든 뒤 잇는다(등록 전의 일은 사건이 아니다. 등록할 때의 Joined가 처음 HP를 알린다).
    internal void Track(CombatWorld world) => world.Events.Subscribe(HandleEvent).AddTo(subscriptions);

    public Skill GetEquippedSkill(int slotIndex) => loadout.GetSlot(slotIndex);

    public float GetSkillCooldownRatio(int slotIndex)
    {
        Skill skill = loadout.GetSlot(slotIndex);
        return skill != null ? skill.CooldownRatio(Now) : 0f;
    }

    // UI(스킬 슬롯 탭)가 부른다. 요청만 남기고, 사거리 안에 대상이 있을 때 다음 결정 시점에 쓴다
    // (달리는 중에 누르면 교전이 시작될 때 쓴다). 위치는 고정 단계 안에서만 읽는다(docs/COMBAT_DESIGN.md 3-2).
    public void RequestSkillSlot(int slotIndex)
    {
        if (!Combatant.IsAlive) return;

        Skill skill = loadout.GetSlot(slotIndex);
        if (skill == null || !skill.IsReadyAt(Now)) return;

        Combatant.Combat.RequestSkill(skill);
    }

    public void Dispose() => subscriptions.Dispose();

    // 슬롯 배치가 바뀌면 다시 맞춘다. 쿨다운은 스킬 id에 붙어 있어서 슬롯을 옮겨도 유지된다(SkillLoadout).
    private void RefreshEquippedSkills()
    {
        loadout.SetSlots(stats.EquippedSlots.Value);
        Combatant.Combat.ClearRequest();
    }

    private void HandleEvent(CombatEvent e)
    {
        if (e.Source == Combatant && e.ChangesHp) hp.Value = new HpState(Combatant.Hp, Combatant.MaxHp);
    }

    // 시뮬레이션 시계. 월드에 등록되기 전에는 시각 0 기준.
    private double Now => Combatant.World != null ? Combatant.World.Clock.Now : 0.0;
}
