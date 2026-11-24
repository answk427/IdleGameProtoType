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

    // 사거리 안에 대상이 생기면 교전한다. 대상 판정은 교전 상태와 같은 것(PlayerController.TryAcquireTarget)을 써서,
    // 두 상태가 서로 다른 기준으로 오가며 떨지 않게 한다. 교전에 들어가면 다음 단계 시작(= 이 단계 끝, 같은 순간)에 첫 결정을 한다.
    public void Execute()
    {
        if (playerController.TryAcquireTarget())
        {
            playerController.fsm.ChangeState(new PlayerCombatState(playerController));
        }
    }

    public void Exit()
    {
        playerController.Stop();
        if (GameManager.Instance != null) GameManager.Instance.IsScrolling = false;
    }
}
