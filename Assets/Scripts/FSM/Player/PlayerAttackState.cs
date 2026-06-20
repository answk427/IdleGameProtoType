using UnityEngine;

public class PlayerAttackState : PlayerCombatState
{
    private float timer;

    public PlayerAttackState(PlayerController playerController, Monster monster) : base(playerController, monster)
    {
    }

    public override void Enter()
    {
        Debug.Log("PlayerAttackState Enter()");
        base.Enter();

        // 들어오자마자 바로 공격할 수 있게
        timer = player.AttackInterval;

        GameManager.Instance.IsScrolling = false;
    }

    protected override void DoAction()
    {
        timer += Time.deltaTime;
        if (timer >= player.AttackInterval)
        {
            player.Attack();
            timer = 0f;
        }
    }

    protected override void OnHitImpact()
    {
        target?.TakeDamage(player.GetCalculatedDamage());
    }
}
