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
        if (GameManager.Instance != null) GameManager.Instance.IsScrolling = true;
    }

    public void Execute()
    {
        Monster target = GameManager.Instance != null ? GameManager.Instance.GetClosestMonster(playerController.transform.position.x) : null;

        if (target != null && CombatRangeUtility.IsWithinAttackRange(
                playerController.transform.position, playerController.HalfWidth, playerController.AttackRange,
                target.transform.position, target.HalfWidth))
        {
            playerController.fsm.ChangeState(new PlayerAttackState(playerController, target));
        }
    }

    public void Exit()
    {
        playerController.Stop();
    }
}
