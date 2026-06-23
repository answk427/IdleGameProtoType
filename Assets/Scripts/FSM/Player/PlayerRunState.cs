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

        // 중심점 사이 거리에서 각자의 반너비를 빼서, 덩치가 큰 몬스터일수록
        // 몸 속으로 들어가지 않고 그 몫만큼 미리 멈춰서 공격하게 한다.
        float edgeGap = (target != null)
            ? (target.transform.position.x - playerController.transform.position.x) - target.HalfWidth - playerController.HalfWidth
            : float.MaxValue;

        if (target != null && edgeGap <= playerController.AttackRange)
        {
            Debug.Log("사거리 안에 들어옴");
            playerController.fsm.ChangeState(new PlayerAttackState(playerController, target));
        }
    }

    public void Exit()
    {
        playerController.Stop();
    }
}
