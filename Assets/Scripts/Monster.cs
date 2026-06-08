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
    public GameObject OriginPrefab { get; set; }

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
    }

    private void OnEnable()
    {
        Debug.Log("Monster OnEnable");
        isAlive = true;
        fsm.ChangeState(new MonsterIdleState(this));
    }

    private void OnDisable()
    {
        if (fsm != null)
        {
            fsm.ChangeState(null);
        }
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
        Debug.Log("Monster Die");
        isAlive = false;
        fsm?.ChangeState(null);
        GameManager.Instance.AddGold(goldReward);
        PlayTrigger("Death");
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
