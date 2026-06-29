using System;
using UnityEngine;

public class Monster : MonoBehaviour, IHasHp, IDamageable
{
    [SerializeField] private FloatingHpBar hpBar;
    [SerializeField] protected Animator animator;

    [SerializeField] protected HitboxProfile hitboxProfile = new();

    [SerializeField] private int maxHp = 10;
    [SerializeField] private int goldReward = 1;
    [SerializeField] private int expReward = 0;

    public event Action<int> OnDamaged;
    public event Action<float, float> OnHpChanged;

    public StateMachine fsm { get; private set; }

    private int currentHp;

    public bool IsAlive { get; protected set; }
    public Vector3 Position => hitboxProfile.GetPosition(transform);
    public float HalfWidth { get; protected set; }
    public int GoldReward => goldReward;
    public int ExpReward => expReward;
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
        IsAlive = true;
        HalfWidth = hitboxProfile.GetHalfWidth(gameObject);
        fsm.ChangeState(new MonsterIdleState(this));

        // 죽으면서 숨겼던 체력바를 풀에서 재사용될 때 다시 보이게 한다.
        if (hpBar != null) hpBar.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        IsAlive = false;
        if (fsm != null)
            fsm.ChangeState(null);
    }

    protected virtual void Update()
    {
        fsm.Update();
    }

    public virtual void Initialize(MonsterData monsterData, float goldMultiplier, float hpMultiplier, float dmgMultiplier)
    {
        this.maxHp = Mathf.RoundToInt(monsterData.MaxHp * hpMultiplier);
        this.currentHp = this.maxHp;
        this.goldReward = Mathf.RoundToInt(monsterData.GoldReward * goldMultiplier);
        this.expReward = monsterData.ExpReward;

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

    // 회복이 필요한 서브클래스(BossMonster 등)가 이 메서드를 통해서만 currentHp를 건드리게 한다.
    protected void HealInternal(int amount)
    {
        if (!IsAlive) return;

        currentHp = Mathf.Min(currentHp + amount, maxHp);
        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    protected virtual void Die()
    {
        IsAlive = false;
        // 골드/경험치 지급은 GlobalCombatEvents.OnMonsterDied 구독자(GameManager)가 처리한다.
        fsm?.ChangeState(new MonsterDieState(this));

        // 시체는 인카운터가 끝날 때까지(ClearEncounter) 풀로 안 돌아가고 화면에 남아있는데,
        // 체력바까지 같이 떠있으면 죽은 몬스터 위에 빈 체력바가 계속 보이는 문제가 있었다.
        if (hpBar != null) hpBar.gameObject.SetActive(false);
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
        float runSpeed = GameManager.Instance != null ? GameManager.Instance.GetPlayer()?.Stats.RunSpeed ?? 0f : 0f;
        transform.Translate(Vector3.left * runSpeed * Time.deltaTime);
    }
}
