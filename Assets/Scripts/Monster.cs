using UnityEngine;

// 몬스터의 호스트. 일반 몬스터와 보스가 모두 이 클래스다(docs/COMBAT_ARCHITECTURE.md 8-3).
// 전투 규칙은 Combatant가 갖고, 둘의 차이는 데이터다:
//   공격 간격 > 0이면 기본 공격, skillId가 있으면 스킬, movePolicy가 Approach면 자기 이동 속도(moveSpeed)로 다가온다.
// 보스인지는 스폰할 때 정하는 역할이고, 처치 알림(BossKilled)에만 쓴다.
public class Monster : CombatantHost
{
    [SerializeField] private FloatingHpBar hpBar;

    private readonly int[] skillSlot = { SkillLoadout.EmptySlot };
    private SkillLoadout skills;
    private GameNumber goldReward;
    private int expReward;
    private bool isBoss;

    protected override Team Team => Team.Enemy;

    // 처치 보상 골드. 스테이지 배율로 지수적으로 커지므로 GameNumber.
    public GameNumber GoldReward => goldReward;
    public int ExpReward => expReward;
    public bool IsBoss => isBoss;
    public GameObject OriginPrefab { get; set; }

    protected override void Awake()
    {
        base.Awake();
        if (hpBar == null) hpBar = GetComponentInChildren<FloatingHpBar>();
        skills = new SkillLoadout(1, id => GameDatabaseManager.Instance?.GetSkill(id));
    }

    private void OnDisable()
    {
        // 정상 경로는 스포너가 풀에 돌려주기 전에 월드에서 뺀다. 다른 경로로 꺼져도 월드에 남지 않게 한다.
        Combatant?.World?.Remove(Combatant);
    }

    // 스포너가 위치를 정한 뒤에 부른다(풀에서 꺼낸 같은 객체를 다시 쓰므로 이전 상태를 모두 지운다).
    public void Spawn(MonsterData data, double hpMultiplier, double damageMultiplier, double goldMultiplier, bool isBoss)
    {
        this.isBoss = isBoss;
        goldReward = GameNumber.Round(data.GoldReward * goldMultiplier);
        expReward = data.ExpReward;

        skillSlot[0] = data.SkillId > 0 ? data.SkillId : SkillLoadout.EmptySlot;
        skills.SetSlots(skillSlot);
        skills.ResetCooldowns();

        var stats = new CombatantStats
        {
            MaxHp = GameNumber.Round(data.MaxHp * hpMultiplier),
            AttackDamage = GameNumber.Round(data.AttackDamage * damageMultiplier),
            AttackInterval = data.AttackInterval,
            AttackRange = data.AttackRange,
            MoveSpeed = data.MoveSpeed, // 접근 정책이 쓴다(제자리 정책은 움직이지 않는다)
            MovePolicy = data.MovePolicy,
        };

        // 체력바가 먼저 켜져 있어야 스폰 때 HP 알림을 받는다.
        if (hpBar != null) hpBar.gameObject.SetActive(true);
        Combatant.Spawn(stats, BodyCenterX(transform.position.x), hitboxProfile.HalfWidth, timelines, skills);
    }

    public override void HandleDied(Combatant self)
    {
        base.HandleDied(self);

        // 시체는 인카운터가 끝날 때까지 화면에 남으므로 빈 체력바를 숨긴다.
        if (hpBar != null) hpBar.gameObject.SetActive(false);

        // 골드/경험치 지급은 GlobalCombatEvents.OnMonsterDied 구독자(GameManager)가 처리한다.
        GlobalCombatEvents.TriggerMonsterDied(this, goldReward, transform.position);
        if (isBoss) GlobalGameEvents.TriggerBossKilled();
    }
}
