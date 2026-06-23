using System;
using UnityEngine;

public class Monster : MonoBehaviour, IHasHp, IDamageable
{
    [SerializeField] private FloatingHpBar hpBar;
    [SerializeField] protected Animator animator;

    [SerializeField] private int maxHp = 10;
    [SerializeField] private int goldReward = 1;
    [SerializeField] protected float moveSpeed = 1.0f;

    // 스프라이트 셀에는 공격 모션 등을 위한 여백이 포함돼 있어 자동 계산(SpriteRenderer/Collider 바운드)이
    // 실제 캐릭터 폭보다 훨씬 크게 잡히는 문제가 있다. 0보다 크면 이 값을 그대로 반너비로 사용한다.
    [SerializeField] protected float halfWidthOverride = 0f;

    public event Action<int> OnDamaged;
    public event Action<float, float> OnHpChanged;

    public StateMachine fsm { get; private set; }

    private int currentHp;
    protected int attackDamage;

    public bool IsAlive { get; protected set; }
    public Vector3 Position => transform.position;
    public float HalfWidth { get; protected set; }
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
        HalfWidth = halfWidthOverride > 0f ? halfWidthOverride : CombatRangeUtility.GetHalfWidth(gameObject);
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
