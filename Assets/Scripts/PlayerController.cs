using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어 캐릭터(전투 상태 + 연출 연결). 저장되는 진행도(PlayerStats)는 이 캐릭터가 갖지 않고
// GameSession이 소유하며, GameInitializer가 Initialize(stats)로 넘겨준다.
//
// 전투 행동의 결정과 진행은 CombatActions(공용, 순수 C#)가 하고, 이 클래스는 그것이 요구하는 것(ICombatActor)을 제공한다.
// 타이밍은 행동 시간표(ActionTimelineSet)에서 오고, 애니메이터는 CharacterAnimationView가 재생만 한다
// (docs/COMBAT_ARCHITECTURE.md 7장). 2단계에서 보스와 함께 Combatant로 합친다.
public class PlayerController : MonoBehaviour, IHasHp, IDamageable, ISkillCaster, ICombatActor, ICombatParticipant
{
    [Header("전투")]
    [SerializeField] private float attackRange = 0.5f;
    [SerializeField] private ActionTimelineSet timelines;
    [SerializeField] private CharacterAnimationView view;

    [Header("이동")]
    [Tooltip("고정 이동속도. 버프/스킬로만 일시적으로 변경됨")]
    [SerializeField] private float baseRunSpeed = 2f;

    // 이동속도는 저장되는 진행도가 아니라 캐릭터 속성이라 PlayerStats가 아니라 여기 둔다.
    // 버프/스킬이 생기면 여기에 배율만 곱하면 된다.
    public float RunSpeed => baseRunSpeed;

    [SerializeField] private HitboxProfile hitboxProfile = new();

    [Tooltip("스프라이트 피벗이 발 기준이 아닐 때, 발이 바닥(PlayAreaBounds.GroundY)에 닿아 보이도록 " +
             "보정하는 값(월드 유닛). MonsterEntry.groundOffset과 동일한 역할 — 캐릭터(직업)별로 다른 " +
             "스프라이트를 쓸 수 있으므로 PlayAreaBounds 쪽 전역값이 아니라 여기서 캐릭터별로 보정한다.")]
    [SerializeField] private float groundOffset = 0f;
    public float GroundOffset => groundOffset;

    private PlayerStats stats;
    private int currentHp;
    private SkillLoadout loadout;
    private Monster currentTarget;

    public event Action<float, float> OnHpChanged;
    public event Action<int> OnDamaged;

    public StateMachine fsm { get; private set; }
    public CombatActions Combat { get; private set; }
    // 시뮬레이션 시계. CombatSimulation이 넘겨준다(사망 시각, UI 쿨다운 표시에 쓴다).
    public SimClock Clock { get; private set; }
    public bool IsAlive { get; private set; }
    public Vector3 Position => hitboxProfile.GetPosition(transform);
    public float HalfWidth { get; private set; }

    public int AttackDamage => stats.AttackDamage;
    public float AttackInterval => stats.AttackInterval;
    public float AttackRange => attackRange;
    public int MaxHp => stats.MaxHp;
    public int CurrentHp => currentHp;
    public PlayerStats Stats => stats;
    public ActionTimelineSet Timelines => timelines;

    private void Awake()
    {
        if (view == null) view = GetComponent<CharacterAnimationView>();

        fsm = new StateMachine();
        Combat = new CombatActions(this);
        loadout = new SkillLoadout(PlayerStatsSaveData.SkillSlotCount, id => GameDatabaseManager.Instance?.GetSkill(id));
        HalfWidth = hitboxProfile.GetHalfWidth(gameObject);

        if (timelines == null) Debug.LogError($"[PlayerController] {name}에 ActionTimelineSet이 연결되지 않았습니다.");
        if (view == null) Debug.LogError($"[PlayerController] {name}에 CharacterAnimationView가 없습니다.");
    }

    // 다른 객체(진행도)에 의존하는 초기화는 Awake가 아니라 GameInitializer가 부르는 여기서 한다.
    // 예전에는 Awake에서 세이브를 직접 읽었는데, 그 때문에 "Awake가 가장 먼저 돌아야 한다"는
    // 암묵적인 순서 규칙이 생겼었다.
    public void Initialize(PlayerStats stats)
    {
        if (this.stats != null)
        {
            this.stats.OnSkillEquipChanged -= RefreshEquippedSkills;
        }

        this.stats = stats;

        currentHp = stats.MaxHp;
        IsAlive = true;

        RefreshEquippedSkills();
        stats.OnSkillEquipChanged += RefreshEquippedSkills;
    }

    private void OnDestroy()
    {
        if (stats != null)
        {
            stats.OnSkillEquipChanged -= RefreshEquippedSkills;
        }
    }

    public void AttachClock(SimClock clock) => Clock = clock;

    // CombatSimulation이 고정 단계의 "상태 전이와 이동" 국면에 부른다(docs/COMBAT_ARCHITECTURE.md 7-11).
    // 행동과 쿨다운의 시간 진행은 여기가 아니라 CombatScheduler가 한다.
    public void Tick(SimClock clock)
    {
        if (stats == null) return; // GameInitializer가 Initialize()하기 전

        Clock = clock;
        fsm.Update();
    }

    public void Run()
    {
        if (view != null) view.PlayLocomotion(true);
    }

    public void Stop()
    {
        if (view != null) view.PlayLocomotion(false);
    }

    public int GetCalculatedDamage(float skillMultiplier = 1.0f)
    {
        // 크리티컬 계산, 버프 계산 등 적용
        float finalDamage = stats.AttackDamage * skillMultiplier;
        return Mathf.RoundToInt(finalDamage);
    }

    public void TakeDamage(int damage)
    {
        if (!IsAlive) return;

        currentHp = Mathf.Max(currentHp - damage, 0);
        OnHpChanged?.Invoke(currentHp, stats.MaxHp);
        OnDamaged?.Invoke(damage);
        GlobalCombatEvents.TriggerAnyTargetDamaged(this, damage, transform.position);

        if (currentHp <= 0)
        {
            Die();
            return;
        }
    }

    public void Die()
    {
        IsAlive = false;
        Combat.ClearRequest();
        fsm?.ChangeState(new PlayerDieState(this));
    }

    public void PlayDeathAnimation()
    {
        if (view != null) view.PlayDeath();
    }

    public void Revive()
    {
        currentHp = stats.MaxHp;
        IsAlive = true;
        if (view != null) view.PlayLocomotion(false);
        OnHpChanged?.Invoke(currentHp, stats.MaxHp);
        fsm?.ChangeState(new PlayerRunState(this));
    }


    // ── 경험치 (몬스터 처치 보상 등에서 호출) ──
    // 업그레이드(골드 차감 + 스탯 증가)는 게임 규칙이라 GameSession.TryUpgrade()로 옮겼다.

    public void AddExp(int amount) => stats.AddExp(amount);

    public void SaveProgress() => GameManager.Instance?.SaveGame();

    // ── 대상 ──

    // 지금 대상이 살아 있고 사거리 안이면 그대로, 아니면 사거리 안에서 다시 찾는다(D3). 찾으면 현재 대상으로 잡는다.
    public bool TryAcquireTarget() => IsAlive && AcquireTarget() != null;

    private Monster AcquireTarget()
    {
        if (IsTargetInRange(currentTarget)) return currentTarget;

        Monster candidate = GameManager.Instance != null ? GameManager.Instance.GetClosestMonster(transform.position.x) : null;
        currentTarget = IsTargetInRange(candidate) ? candidate : null;
        return currentTarget;
    }

    private bool IsTargetInRange(Monster target) =>
        target != null && target.IsAlive && CombatRangeUtility.IsWithinAttackRange(
            transform.position, HalfWidth, AttackRange, target.transform.position, target.HalfWidth);

    // ── ICombatActor ──

    public bool TryGetTimeline(string key, out ActionTimeline timeline)
    {
        timeline = null;
        return timelines != null && timelines.TryGet(key, out timeline);
    }

    public double BasicAttackDuration => stats.AttackInterval;

    public Skill PickReadySkill(double now) => loadout.PickReady(now);

    public double NextDecisionInputChange(double now) => loadout.NextReadyTime(now);

    public void ApplyBasicAttackHit()
    {
        Monster target = AcquireTarget();
        if (target == null) return; // 헛침

        target.TakeDamage(GetCalculatedDamage());
        if (CombatEffectManager.Instance != null) CombatEffectManager.Instance.SpawnHitParticle(target);
    }

    public void ApplySkillHit(Skill skill)
    {
        // 단일 대상 효과는 대상을 다시 확인하고, 범위·자기 효과는 대상이 없어도 적용된다(효과 구현이 판단).
        Vector3[] hitPositions = skill.Apply(this, AcquireTarget());
        SkillPresentation.PlayHit(skill, hitPositions);
    }

    public void OnActionStarted(ActionTimeline action, double duration, Skill skill)
    {
        if (view != null) view.PlayAction(action, (float)duration);
        if (skill != null) SkillPresentation.PlayCast(skill, transform.position);
    }

    public void OnActionsIdle()
    {
        if (view != null) view.PlayLocomotion(false);
    }

    // ── 스킬 시스템 ──

    // 슬롯 배치가 바뀌면 다시 맞춘다. 쿨다운은 스킬 id에 붙어 있어서 슬롯을 옮겨도 유지된다(SkillLoadout).
    public void RefreshEquippedSkills()
    {
        loadout.SetSlots(stats.EquippedSlots.Value);
        Combat.ClearRequest();
    }

    // 범위 스킬(AOE)이 GameManager를 직접 몰라도 되도록 caster를 통해서만 몬스터 목록을 받아간다.
    public List<Monster> GetMonstersInRange(float radius)
    {
        if (GameManager.Instance == null) return new List<Monster>();
        return GameManager.Instance.GetMonstersInRange(transform.position.x, radius);
    }

    public List<IDamageable> GetOpponentsInRange(float radius)
    {
        List<Monster> monsters = GetMonstersInRange(radius);
        List<IDamageable> result = new List<IDamageable>(monsters.Count);
        for (int i = 0; i < monsters.Count; i++)
        {
            result.Add(monsters[i]);
        }
        return result;
    }

    public Skill GetEquippedSkill(int slotIndex) => loadout.GetSlot(slotIndex);

    // UI 쿨다운 표시. 시계가 붙기 전에는 시각 0 기준.
    public float GetSkillCooldownRatio(int slotIndex)
    {
        Skill skill = loadout.GetSlot(slotIndex);
        return skill != null ? skill.CooldownRatio(Clock != null ? Clock.Now : 0.0) : 0f;
    }

    // UI(스킬 슬롯 클릭)가 호출한다. 요청만 남기고, 사거리 안에 대상이 있을 때 다음 결정 시점에 쓴다
    // (달리는 중에 누르면 교전이 시작될 때 쓴다). 위치는 고정 단계 안에서만 읽는다(docs/COMBAT_DESIGN.md 3-2).
    public void RequestSkillSlot(int slotIndex)
    {
        if (!IsAlive) return;

        Skill skill = loadout.GetSlot(slotIndex);
        if (skill == null || !skill.IsReadyAt(Clock != null ? Clock.Now : 0.0)) return;

        Combat.RequestSkill(skill);
    }

    public void Heal(int amount)
    {
        currentHp = Mathf.Min(currentHp + amount, stats.MaxHp);
        OnHpChanged?.Invoke(currentHp, stats.MaxHp);
        GlobalCombatEvents.TriggerHealed(amount, transform.position);
    }

    public void FullHeal() => Heal(stats.MaxHp);
}
