using System;
using UnityEngine;

public class PlayerController : MonoBehaviour, IHasHp
{
    [SerializeField] private PlayerStats stats = new PlayerStats();
    [SerializeField] private Animator animator;
    [SerializeField] private string runBoolName = "";
    [SerializeField] private string attackTriggerName = "";
    [SerializeField] private float attackRange = 0.5f;

    public event Action OnHitEvent;
    public event Action OnAttackEndEvent;
    public event Action<float, float> OnHpChanged;

    public StateMachine fsm { get; private set; }
    public bool IsAlive { get; private set; }

    public int AttackDamage => stats.AttackDamage;
    public float AttackInterval => stats.AttackInterval;
    public float AttackRange => attackRange;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        IsAlive = true;
        fsm = new StateMachine();
    }

    private void Update()
    {
        fsm.Update();
    }   

    public void Run()
    {
        Debug.Log("Run");
        SetRunAnimation(true);
    }

    public void Stop()
    {
        SetRunAnimation(false);
    }

    public void Attack()
    {
        Debug.Log("Attack");

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

    //애니메이션 이벤트가 호출할 함수(애니메이션 끝난 시점)"
    public void AE_OnAttackEnd()
    {
        OnAttackEndEvent?.Invoke();
    }
}
