using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerStats stats = new PlayerStats();
    [SerializeField] private Animator animator;
    [SerializeField] private string runBoolName = "";
    [SerializeField] private string attackTriggerName = "";
    [SerializeField] private float attackRange = 0.5f;

    public StateMachine fsm { get; private set; }

    public int AttackDamage => stats.AttackDamage;
    public float AttackInterval => stats.AttackInterval;
    public float AttackRange { get; }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

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

    public void Attack(Monster target)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }

        Debug.Log("Attack");

        PlayAttackAnimation();
        target.TakeDamage(stats.AttackDamage);
    }

    private void SetRunAnimation(bool value)
    {
        if (animator != null && !string.IsNullOrEmpty(runBoolName))
        {
            animator.SetBool(runBoolName, value);
        }
    }

    private void PlayAttackAnimation()
    {
        if (animator != null && !string.IsNullOrEmpty(attackTriggerName))
        {
            animator.SetTrigger(attackTriggerName);
        }
    }
}
