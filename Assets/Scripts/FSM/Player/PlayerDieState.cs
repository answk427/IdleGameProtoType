using System.Threading;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class PlayerDieState : IState
{
    private PlayerController player;

    public PlayerDieState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        player.PlayDeathAnimation();
        player.OnDeathEndEvent += HandleDeathAniEnd;
    }

    public void Execute()
    {
    }

    public void Exit()
    {
        player.OnDeathEndEvent -= HandleDeathAniEnd;
    }

    private void HandleDeathAniEnd()
    {
        GlobalGameEvents.TriggerPlayerDied();
    }
}
