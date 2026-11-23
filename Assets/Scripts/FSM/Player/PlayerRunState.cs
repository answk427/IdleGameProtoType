using UnityEngine;

public class PlayerRunState : IState
{
    private PlayerController playerController;

    public PlayerRunState(PlayerController playerController)
    {
        this.playerController = playerController;
    }

    // 스크롤의 주인은 달리기 상태다: 들어올 때 켜고 나갈 때 끈다(docs/COMBAT_DESIGN.md 3-5).
    // 예전에는 공격 상태가 껐기 때문에, 달리다 바로 수동 스킬로 가면 시전 중에도 세상이 흘러 몬스터가 몸을 통과했다.
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
        if (GameManager.Instance != null) GameManager.Instance.IsScrolling = false;
    }
}
