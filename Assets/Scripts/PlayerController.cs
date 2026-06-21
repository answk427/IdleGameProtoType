using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour, IHasHp, IDamageable
{
    [Header("스탯 설정 (ScriptableObject)")]
    [SerializeField] private PlayerStatDatabase statData;
    [SerializeField] private PlayerUpgradeConfig upgradeConfig;

    [Header("애니메이션")]
    [SerializeField] private Animator animator;
    [SerializeField] private string runBoolName = "Run";
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private string deathTriggerName = "Death";
    [SerializeField] private string reviveTriggerName = "Revive";
    [SerializeField] private float attackRange = 0.5f;

    private PlayerStats stats = new PlayerStats();
    private int currentHp;

    private readonly List<Skill> skills = new List<Skill>();

    public event Action OnHitEvent;
    public event Action OnAttackEndEvent;
    public event Action OnDeathEndEvent;
    public event Action<float, float> OnHpChanged;
    public event Action<int> OnDamaged;

    public StateMachine fsm { get; private set; }
    public bool IsAlive { get; private set; }

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

        fsm = new StateMachine();

        stats.Initialize(statData, upgradeConfig);
        stats.LoadSave(PlayerSaveData.Load());

        currentHp = stats.MaxHp;
        IsAlive = true;

        if (GameDatabaseManager.Instance != null)
        {
            List<SkillData> skillDatas = new List<SkillData>();
            foreach (var entry in GameDatabaseManager.Instance.GetAllSkills())
            {
                if (entry?.data != null) skillDatas.Add(entry.data);
            }
            InitializeSkills(skillDatas);
        }
    }

    private void Update()
    {
        fsm.Update();

        for (int i = 0; i < skills.Count; i++)
        {
            skills[i].Tick(Time.deltaTime);
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
        PlayTrigger(attackTriggerName);
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
        Debug.Log($"Player TakeDamage {damage}, currentHp : {currentHp}");
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


    // ── 경험치 / 업그레이드 (외부 UI, 몬스터 처치 보상 등에서 호출) ──

    public void AddExp(int amount) => stats.AddExp(amount);

    public void SaveProgress() => stats.Save();

    public bool TryUpgradeHp()
    {
        if (GameManager.Instance == null) return false;
        if (!GameManager.Instance.TrySpendGold(stats.NextHpUpgradeCost)) return false;
        stats.UpgradeHp();
        stats.Save();
        return true;
    }

    public bool TryUpgradeAttack()
    {
        if (GameManager.Instance == null) return false;
        if (!GameManager.Instance.TrySpendGold(stats.NextAttackUpgradeCost)) return false;
        stats.UpgradeAttack();
        stats.Save();
        return true;
    }

    public bool TryUpgradeSpeed()
    {
        if (GameManager.Instance == null) return false;
        if (!GameManager.Instance.TrySpendGold(stats.NextSpeedUpgradeCost)) return false;
        stats.UpgradeSpeed();
        stats.Save();
        return true;
    }

    // ── 스킬 시스템 ──

    public void InitializeSkills(List<SkillData> skillDatas)
    {
        skills.Clear();
        foreach (var data in skillDatas)
        {
            skills.Add(new Skill(data));
        }
    }

    public Skill GetReadySkill()
    {
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i].IsReady) return skills[i];
        }
        return null;
    }

    public void Heal(int amount)
    {
        currentHp = Mathf.Min(currentHp + amount, stats.MaxHp);
        OnHpChanged?.Invoke(currentHp, stats.MaxHp);
    }
}
