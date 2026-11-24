using System.Collections.Generic;
using UnityEngine;

// 보스. 전투 행동의 결정과 진행은 플레이어와 같은 CombatActions가 하고, 이 클래스는 ICombatActor를 제공한다.
// 기본 공격 한 번의 길이는 데이터의 공격 간격(MonsterData.AttackInterval)이고, 클립은 그 길이에 맞춰 재생된다
// (docs/COMBAT_ARCHITECTURE.md 7장). 2단계에서 플레이어와 함께 Combatant로 합친다.
public class BossMonster : Monster, ISkillCaster, ICombatActor, ICombatParticipant
{
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackInterval = 2.0f;
    [SerializeField] private ActionTimelineSet timelines;

    private int attackDamage;
    private Skill skill;

    public float AttackRange => attackRange;
    public float AttackInterval => attackInterval;
    public int AttackDamage => attackDamage;
    public Skill Skill => skill;
    public CombatActions Combat { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        Combat = new CombatActions(this);
        if (timelines == null) Debug.LogError($"[BossMonster] {name}에 ActionTimelineSet이 연결되지 않았습니다.");
    }

    protected override void OnEnable()
    {
        IsAlive = true;
        HalfWidth = hitboxProfile.GetHalfWidth(gameObject);
        fsm.ChangeState(new BossRunState(this));
    }

    protected override void Die()
    {
        base.Die();
        GlobalGameEvents.TriggerBossKilled();
    }

    public override void Initialize(MonsterData monsterData, float goldMultiplier, float hpMultiplier, float dmgMultiplier)
    {
        base.Initialize(monsterData, goldMultiplier, hpMultiplier, dmgMultiplier);
        this.attackRange = monsterData.AttackRange;
        this.attackInterval = monsterData.AttackInterval;
        this.attackDamage = Mathf.RoundToInt(monsterData.AttackDamage * dmgMultiplier);

        SkillEntry entry = monsterData.SkillId > 0 ? GameDatabaseManager.Instance?.GetSkill(monsterData.SkillId) : null;
        this.skill = entry?.data != null ? new Skill(entry) : null;

        // 데이터가 가리키는 스킬이 없으면 스킬 없이 싸운다. 조용히 넘어가지 않고 알린다.
        if (monsterData.SkillId > 0 && this.skill == null)
            Debug.LogError($"[BossMonster] {monsterData.MonsterName}의 스킬 {monsterData.SkillId}이 SkillDatabase에 없습니다. 스킬 없이 싸웁니다.");
    }

    public int GetCalculatedDamage()
    {
        return attackDamage;
    }

    public int GetCalculatedDamage(float skillMultiplier)
    {
        return Mathf.RoundToInt(attackDamage * skillMultiplier);
    }

    public void Heal(int amount) => HealInternal(amount);

    // 보스의 적은 항상 플레이어 1명뿐이라 단순 거리 체크로 충분.
    public List<IDamageable> GetOpponentsInRange(float radius)
    {
        List<IDamageable> result = new List<IDamageable>();
        PlayerController player = GameManager.Instance != null ? GameManager.Instance.GetPlayer() : null;

        if (player != null && Mathf.Abs(player.transform.position.x - transform.position.x) <= radius)
        {
            result.Add(player);
        }

        return result;
    }

    // ── 대상 ──

    // 플레이어가 살아 있고 사거리 안이면 대상, 아니면 null.
    public PlayerController AcquireTarget()
    {
        PlayerController player = GameManager.Instance != null ? GameManager.Instance.GetPlayer() : null;
        if (player == null || !player.IsAlive) return null;

        return CombatRangeUtility.IsWithinAttackRange(transform.position, HalfWidth, attackRange, player.transform.position, player.HalfWidth)
            ? player
            : null;
    }

    // ── ICombatActor ──

    public bool TryGetTimeline(string key, out ActionTimeline timeline)
    {
        timeline = null;
        return timelines != null && timelines.TryGet(key, out timeline);
    }

    public double BasicAttackDuration => attackInterval;

    public bool TryAcquireTarget() => IsAlive && AcquireTarget() != null;

    public Skill PickReadySkill(double now) => skill != null && skill.IsReadyAt(now) ? skill : null;

    public double NextDecisionInputChange(double now) =>
        skill != null && skill.ReadyAt > now ? skill.ReadyAt : double.PositiveInfinity;

    public void ApplyBasicAttackHit()
    {
        PlayerController target = AcquireTarget();
        if (target == null) return; // 헛침

        target.TakeDamage(GetCalculatedDamage());
        if (CombatEffectManager.Instance != null) CombatEffectManager.Instance.SpawnHitParticle(target);
    }

    public void ApplySkillHit(Skill castSkill)
    {
        Vector3[] hitPositions = castSkill.Apply(this, AcquireTarget());
        SkillPresentation.PlayHit(castSkill, hitPositions);
    }

    public void OnActionStarted(ActionTimeline action, double duration, Skill castSkill)
    {
        if (view != null) view.PlayAction(action, (float)duration);
        if (castSkill != null) SkillPresentation.PlayCast(castSkill, transform.position);
    }

    public void OnActionsIdle()
    {
        if (view != null) view.PlayLocomotion(false);
    }
}
