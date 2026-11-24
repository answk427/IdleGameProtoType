// 교전 상태(docs/COMBAT_ARCHITECTURE.md 7-4, 7-11).
// "교전 중"이라는 표시만 한다. 기본 공격과 스킬의 결정·진행은 CombatActions가 하고, 시간 진행은 CombatScheduler가
// 모든 전투원의 사건을 시각 순서로 처리하며 한다. 이 상태는 시간을 진행하지 않는다.
// 예전의 공격 상태와 스킬 상태(애니메이션 끝 이벤트를 기다리던 isAnimationFinished 방식)를 이 상태 하나가 대신한다.
// 지금 행동이 끝났고(취소 없음, 7-13) 사거리 안에 대상이 없으면 달리기로 돌아간다(상태 전이 국면, 단계 단위).
public class PlayerCombatState : IState
{
    private readonly PlayerController player;

    public PlayerCombatState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        player.Combat.Engage();
    }

    public void Execute()
    {
        if (player.Combat.IsFree && !player.TryAcquireTarget())
        {
            player.fsm.ChangeState(new PlayerRunState(player));
        }
    }

    public void Exit()
    {
        player.Combat.Disengage();
    }
}
