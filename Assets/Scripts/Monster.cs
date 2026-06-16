using System;
using UnityEngine;

public class Monster : MonoBehaviour, IHasHp, IDamageable
{
    [SerializeField] private FloatingHpBar hpBar;
    [SerializeField] protected Animator animator;

    [SerializeField] private int maxHp = 10;
    [SerializeField] private int goldReward = 1;
    [SerializeField] protected float moveSpeed = 1.0f;

    public event Action<int> OnDamaged;
    public event Action<float, float> OnHpChanged;

    public StateMachine fsm { get; private set; }

    private int currentHp;
    protected int attackDamage;

    public bool IsAlive { get; protected set; }
    public int GoldReward => goldReward;
    public GameObject OriginPrefab { get; set; }

    protected virtual void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (hpBar == null)  
            hpBar = GetComponentInChildren<FloatingHpBar>();

        fsm = new StateMachine();

        currentHp = maxHp;
        IsAlive = true;
    }

    protected virtual void OnEnable()
    {
        Debug.Log("Monster OnEnable");
        IsAlive = true;
        fsm.ChangeState(new MonsterIdleState(this));
    }

    private void OnDisable()
    {
        IsAlive = false;
        if (fsm != null)
            fsm.ChangeState(null);
    }

    private void Update()
    {
        fsm.Update();
    }

    public virtual void Initialize(MonsterData monsterData, float goldMultiplier, float hpMultiplier, float dmgMultiplier)
    {
        this.maxHp = Mathf.RoundToInt(monsterData.maxHp * hpMultiplier);
        this.currentHp = this.maxHp;
        this.attackDamage = Mathf.RoundToInt(monsterData.attackDamage * dmgMultiplier);
        this.goldReward = Mathf.RoundToInt(monsterData.goldReward * goldMultiplier);

        IsAlive = true;
        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    public void TakeDamage(int damage)
    {
        if (!IsAlive) return;

        currentHp = Mathf.Max(currentHp - damage, 0);
        OnHpChanged?.Invoke(currentHp, maxHp);
        OnDamaged?.Invoke(damage);
        GlobalCombatEvents.TriggerAnyTargetDamaged(this, damage, transform.position);

        if (currentHp <= 0)
        {
            Die();
            return;
        }
    }

    protected virtual void Die()
    {
        Debug.Log("Monster Die");
        IsAlive = false;
        GameManager.Instance.AddGold(goldReward);
        fsm?.ChangeState(new MonsterDieState(this));
    }

    protected void PlayTrigger(string triggerName)
    {
        if (animator != null)
            animator.SetTrigger(triggerName);
    }

    public void PlayIdleAnimation()
    {
        animator.Play("Idle");
    }

    public void PlayDeathAnimation()
    {
        PlayTrigger("Death");
    }

    public void moveToPlayer()
    {
        transform.Translate(Vector3.left * moveSpeed * Time.deltaTime);
    }
}
