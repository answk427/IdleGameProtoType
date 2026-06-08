using UnityEngine;

public class PlayerRunState : IState
{
    private PlayerController playerController;

    public PlayerRunState(PlayerController playerController)
    {
        this.playerController = playerController;

    }

    public void Enter()
    {
        playerController.Run();
        GameManager.Instance.IsScrolling = true;
    }

    public void Execute()
    {
        Monster target = GameManager.Instance.GetClosestMonster(playerController.transform.position.x);

        if (target == null)
        {
            //Debug.Log("target is null");
        }

        if (target != null && (target.transform.position.x - playerController.transform.position.x)
            <= playerController.AttackRange)
        {
            Debug.Log("사거리 안에 들어옴");
            // 사거리에 들어오면 공격 상태로 변경!
            playerController.fsm.ChangeState(new PlayerAttackState(playerController, target));
        }
    }

    public void Exit()
    {
        playerController.Stop();
    }
}
