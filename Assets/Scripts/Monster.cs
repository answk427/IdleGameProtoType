using System.Threading;
using UnityEngine;

public class Monster : MonoBehaviour
{
    [SerializeField] private int maxHp = 10;
    [SerializeField] private int goldReward = 1;
    [SerializeField] private float destroyDelay = 0.5f;
    [SerializeField] private Animator animator;
    [SerializeField] private float moveSpeed = 1.0f;
    
    public StateMachine fsm { get; private set; }

    private int currentHp;
    private bool isAlive = true;

    public bool IsAlive => isAlive;
    public int GoldReward => goldReward;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        fsm = new StateMachine();

        currentHp = maxHp;
        isAlive = true;
    }

    private void Start()
    {
        Debug.Log("Monster Start");
        fsm.ChangeState(new MonsterIdleState(this));
    }

    private void Update()
    {
        fsm.Update();
    }

    public void Initialize(int hp, int reward)
    {
        maxHp = hp;
        goldReward = reward;
        currentHp = maxHp;
        isAlive = true;
    }

    public void TakeDamage(int damage)
    {
        if (!isAlive)
        {
            return;
        }

        currentHp = Mathf.Max(currentHp - damage, 0);

        if (currentHp <= 0)
        {
            Die();
            return;
        }

        PlayTrigger("Hurt");
    }

    private void Die()
    {
        isAlive = false;
        PlayTrigger("Death");
        Destroy(gameObject, destroyDelay);
    }

    private void PlayTrigger(string triggerName)
    {
        if (animator != null)
        {
            animator.SetTrigger(triggerName);
        }
    }

    public void PlayIdleAnimation()
    {
        animator.Play("Idle");
    }

    public void moveToPlayer()
    {
        transform.Translate(Vector3.left * moveSpeed * Time.deltaTime);
    }
}
