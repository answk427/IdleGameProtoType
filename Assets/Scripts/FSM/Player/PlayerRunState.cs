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

        if (target != null && (target.transform.position.x - playerController.transform.position.x) <= playerController.AttackRange)
        {
            // 사거리에 들어오면 공격 상태로 변경!
            playerController.fsm.ChangeState(new PlayerAttackState(playerController, target));
        }
    }

    public void Exit()
    {
        playerController.Stop();
    }
}
