using UnityEngine;

public class PlayerAttackState : IState
{
    private PlayerController playerController;
    private Monster target;
    private float timer;

    public PlayerAttackState(PlayerController playerController, Monster monster)
    {
        this.playerController = playerController;
        this.target = monster;
    }

    public void Enter()
    {
        //시작하자마자 바로 때리게 세팅
        timer = playerController.AttackInterval;

        GameManager.Instance.IsScrolling = false;
    }

    public void Execute()
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }

        timer += Time.deltaTime;
        if (timer >= playerController.AttackInterval)
        {
            playerController.Attack(target);
            timer = 0f;
        }
    }

    public void Exit()
    {
        
    }
}
