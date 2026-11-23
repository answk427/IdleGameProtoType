using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어 캐릭터(전투 상태 + 연출). 저장되는 진행도(PlayerStats)는 이 캐릭터가 갖지 않고
// GameSession이 소유하며, GameInitializer가 Initialize(stats)로 넘겨준다.
public class PlayerController : MonoBehaviour, IHasHp, IDamageable, ISkillCaster
{
    [Header("애니메이션")]
    [SerializeField] private Animator animator;
    [SerializeField] private string runBoolName = "Run";
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private string deathTriggerName = "Death";
    [SerializeField] private string reviveTriggerName = "Revive";
    [SerializeField] private float attackRange = 0.5f;

    // Attack 애니메이터 스테이트의 Speed가 이 파라미터를 참조하도록 설정돼 있어야 한다.
    // 공격속도 업그레이드로 AttackInterval이 짧아져도 애니메이션 자체가 안 빨라지면
    // 클립 길이에 막혀서 더 이상 안 빨라지는 것처럼 보이는 문제가 있었다.
    private const string AttackSpeedParam = "AttackSpeedMultiplier";
    private float attackClipLength = 0f;

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

    // 슬롯 인덱스 → 장착된 Skill 런타임 인스턴스 (빈 슬롯은 null)
    private readonly Skill[] equippedSkills = new Skill[PlayerStatsSaveData.SkillSlotCount];

    // UI가 요청한 스킬 슬롯. 다음 Tick에서 처리한다.
    private const int NoPendingSkill = -1;
    private int pendingSkillSlot = NoPendingSkill;

    public event Action OnHitEvent;
    public event Action OnAttackEndEvent;
    public event Action OnDeathEndEvent;
    public event Action<float, float> OnHpChanged;
    public event Action<int> OnDamaged;

    public StateMachine fsm { get; private set; }
    public bool IsAlive { get; private set; }
    public Vector3 Position => hitboxProfile.GetPosition(transform);
    public float HalfWidth { get; private set; }

    public int AttackDamage => stats.AttackDamage;
    public float AttackInterval => stats.AttackInterval;
    public float AttackRange => attackRange;
    public int MaxHp => stats.MaxHp;
    public int CurrentHp => currentHp;
    public PlayerStats Stats => stats;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        attackClipLength = FindClipLength(attackTriggerName);

        fsm = new StateMachine();
        HalfWidth = hitboxProfile.GetHalfWidth(gameObject);
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

    // CombatSimulation이 고정 단계마다 부른다(docs/COMBAT_DESIGN.md 3-1). 안의 Time.deltaTime은 고정 단계 길이다.
    public void Tick()
    {
        if (stats == null) return; // GameInitializer가 Initialize()하기 전

        // UI에서 들어온 스킬 요청은 위치(대상 선택)를 읽으므로 단계 안에서 처리한다.
        if (pendingSkillSlot != NoPendingSkill)
        {
            int slot = pendingSkillSlot;
            pendingSkillSlot = NoPendingSkill;
            UseSkillSlot(slot);
        }

        fsm.Update();

        for (int i = 0; i < equippedSkills.Length; i++)
        {
            equippedSkills[i]?.Tick(Time.deltaTime);
        }
    }

    public void Run()
    {
        SetRunAnimation(true);
    }

    public void Stop()
    {
        SetRunAnimation(false);
    }

    public void Attack()
    {
        if (animator != null && attackClipLength > 0f)
        {
            // 애니메이션이 정확히 AttackInterval 안에 끝나도록 재생 속도를 매번 다시 맞춘다.
            float multiplier = attackClipLength / Mathf.Max(0.01f, stats.AttackInterval);
            animator.SetFloat(AttackSpeedParam, multiplier);
        }

        PlayTrigger(attackTriggerName);
    }

    // 런타임 컨트롤러에 들어있는 애니메이션 클립 중 이름이 일치하는 것의 길이(초)를 찾는다.
    private float FindClipLength(string clipName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return 0f;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == clipName) return clip.length;
        }

        return 0f;
    }

    private void SetRunAnimation(bool value)
    {
        if (animator != null && !string.IsNullOrEmpty(runBoolName))
        {
            animator.SetBool(runBoolName, value);
        }
    }

    private void PlayTrigger(string triggerName)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }

    public void PlayAnimationTrigger(string triggerName)
    {
        PlayTrigger(triggerName);
    }

    public int GetCalculatedDamage(float skillMultiplier = 1.0f)
    {
        // 크리티컬 계산, 버프 계산 등 적용
        float finalDamage = stats.AttackDamage * skillMultiplier;
        return Mathf.RoundToInt(finalDamage);
    }

    //애니메이션 이벤트가 호출할 함수 (Hit 시점)
    public void AE_OnHit()
    {
        OnHitEvent?.Invoke();
    }

    //애니메이션 이벤트가 호출할 함수(애니메이션 끝난 시점)
    public void AE_OnAttackEnd()
    {
        OnAttackEndEvent?.Invoke();
    }

    public void AE_OnDeathEnd()
    {
        OnDeathEndEvent?.Invoke();
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
        fsm?.ChangeState(new PlayerDieState(this));
    }

    public void PlayDeathAnimation()
    {
        PlayTrigger(deathTriggerName);
    }

    public void Revive()
    {
        currentHp = stats.MaxHp;
        IsAlive = true;
        PlayTrigger(reviveTriggerName);
        OnHpChanged?.Invoke(currentHp, stats.MaxHp);
        fsm?.ChangeState(new PlayerRunState(this));
    }


    // ── 경험치 (몬스터 처치 보상 등에서 호출) ──
    // 업그레이드(골드 차감 + 스탯 증가)는 게임 규칙이라 GameSession.TryUpgrade()로 옮겼다.

    public void AddExp(int amount) => stats.AddExp(amount);

    public void SaveProgress() => GameManager.Instance?.SaveGame();

    // ── 스킬 시스템 ──

    // 슬롯에 배치된 스킬만 PlayerStats(저장 데이터)을 기준으로 다시 구성한다.
    // 실제로 바뀐 슬롯만 교체해서, 관련 없는 슬롯의 쿨다운이 같이 초기화되지 않게 한다.
    public void RefreshEquippedSkills()
    {
        for (int slot = 0; slot < equippedSkills.Length; slot++)
        {
            int skillId = stats.GetEquippedSkillId(slot);

            if (skillId == PlayerStatsSaveData.EmptySlot)
            {
                equippedSkills[slot] = null;
                continue;
            }

            if (equippedSkills[slot] != null && equippedSkills[slot].Data.Id == skillId) continue;

            SkillEntry entry = GameDatabaseManager.Instance?.GetSkill(skillId);
            equippedSkills[slot] = entry?.data != null ? new Skill(entry) : null;
        }
    }

    public Skill GetReadySkill()
    {
        for (int i = 0; i < equippedSkills.Length; i++)
        {
            if (equippedSkills[i] != null && equippedSkills[i].IsReady) return equippedSkills[i];
        }
        return null;
    }

    private Monster GetSkillTarget()
    {
        if (GameManager.Instance == null) return null;
        return GameManager.Instance.GetClosestMonster(transform.position.x);
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

    public Skill GetEquippedSkill(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= equippedSkills.Length) return null;
        return equippedSkills[slotIndex];
    }

    // UI(스킬 슬롯 클릭)가 호출. 바로 쓰지 않고 다음 고정 단계의 Tick에서 쓴다(docs/COMBAT_DESIGN.md 3-2).
    // 대상 선택이 몬스터 위치를 읽는데, 화면 프레임에서는 위치가 보간된 값이라서.
    // 한 단계(최대 0.02초) 안에 여러 번 누르면 마지막 요청만 남는다.
    public void RequestSkillSlot(int slotIndex)
    {
        pendingSkillSlot = slotIndex;
    }

    private bool UseSkillSlot(int slotIndex)
    {
        if (!IsAlive) return false;
        if (slotIndex < 0 || slotIndex >= equippedSkills.Length) return false;

        Skill skill = equippedSkills[slotIndex];
        if (skill == null || !skill.IsReady) return false;

        Monster target = GetSkillTarget();
        if (target == null) return false;

        fsm.ChangeState(new PlayerSkillState(this, target, skill));
        return true;
    }

    public void Heal(int amount)
    {
        currentHp = Mathf.Min(currentHp + amount, stats.MaxHp);
        OnHpChanged?.Invoke(currentHp, stats.MaxHp);
        GlobalCombatEvents.TriggerHealed(amount, transform.position);
    }

    public void FullHeal() => Heal(stats.MaxHp);
}
