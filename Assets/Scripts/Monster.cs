using System;
using UnityEngine;

public class Monster : MonoBehaviour, IHasHp, IDamageable
{
    [SerializeField] private FloatingHpBar hpBar;
    [SerializeField] protected Animator animator;

    [SerializeField] protected HitboxProfile hitboxProfile = new();

    [SerializeField] private int maxHp = 10;
    [SerializeField] private int expReward = 0;

    public event Action<int> OnDamaged;
    public event Action<float, float> OnHpChanged;

    public StateMachine fsm { get; private set; }

    private int currentHp;

    // 처치 보상 골드. 스테이지 배율로 지수적으로 커지므로 GameNumber. 스폰 시 Initialize()가 데이터로 채운다.
    private GameNumber goldReward = 1;

    // 보간용 직전/현재 고정 단계의 위치
    private Vector3 previousSimulatedPosition;
    private Vector3 currentSimulatedPosition;

    public bool IsAlive { get; protected set; }
    public Vector3 Position => hitboxProfile.GetPosition(transform);
    public float HalfWidth { get; protected set; }
    public GameNumber GoldReward => goldReward;
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

    // CombatSimulation이 고정 단계마다 부른다(docs/COMBAT_DESIGN.md 3-1).
    public virtual void Tick()
    {
        fsm.Update();
    }

    // ── 보간(docs/COMBAT_DESIGN.md 3-3) ──
    // 전투 로직은 transform.position을 그대로 읽고 쓴다. CombatSimulation이 단계 앞에서 시뮬레이션 위치로 되돌리고(Restore),
    // 단계 뒤에서 기록하며(Capture), 화면 프레임에서는 직전 두 단계 사이를 그린다(ApplyRenderPose).

    public void RestoreSimulatedPose() => transform.position = currentSimulatedPosition;

    public void CaptureSimulatedPose()
    {
        previousSimulatedPosition = currentSimulatedPosition;
        currentSimulatedPosition = transform.position;
    }

    public void ApplyRenderPose(float alpha) =>
        transform.position = Vector3.LerpUnclamped(previousSimulatedPosition, currentSimulatedPosition, alpha);

    // 위치를 직접 정한 직후(스폰)에 부른다. 안 하면 풀에서 꺼낸 몬스터가 예전 자리에서 미끄러져 오는 게 보인다.
    private void SnapSimulatedPose()
    {
        previousSimulatedPosition = transform.position;
        currentSimulatedPosition = transform.position;
    }

    // 스포너가 위치를 정한 뒤에 부른다(PoolManager.Spawn이 위치를 정하는 건 OnEnable 이후라 여기서 맞춘다).
    public virtual void Initialize(MonsterData monsterData, float goldMultiplier, float hpMultiplier, float dmgMultiplier)
    {
        this.maxHp = Mathf.RoundToInt(monsterData.MaxHp * hpMultiplier);
        this.currentHp = this.maxHp;
        this.goldReward = GameNumber.Round((double)monsterData.GoldReward * goldMultiplier);
        this.expReward = monsterData.ExpReward;

        IsAlive = true;
        OnHpChanged?.Invoke(currentHp, maxHp);
        SnapSimulatedPose();
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
        PlayerController player = GameManager.Instance != null ? GameManager.Instance.GetPlayer() : null;
        if (player == null) return;

        Vector3 position = transform.position;
        float next = position.x - player.RunSpeed * Time.deltaTime;

        // CCD(docs/COMBAT_DESIGN.md 3-4): 살아 있는 몬스터는 플레이어 몸을 통과하지 못한다.
        // 접촉선(두 몸이 닿는 위치) 앞이면 거기서 멈추고, 이미 겹쳐 있으면(순간이동 등) 더 파고들지 않는다.
        // 한 단계 이동량과 상관없이 성립하고, 접촉선에서는 표면 사이 거리가 0이라 교전이 반드시 시작된다.
        float contactX = player.transform.position.x + player.HalfWidth + HalfWidth;
        position.x = Mathf.Max(next, Mathf.Min(position.x, contactX));

        transform.position = position;
    }
}
