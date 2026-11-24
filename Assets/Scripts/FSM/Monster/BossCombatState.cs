// 보스의 교전 상태(docs/COMBAT_ARCHITECTURE.md 7-4, 7-11). 플레이어와 같은 방식이다.
// "교전 중"이라는 표시만 하고, 행동의 결정·진행은 CombatActions가, 시간 진행은 CombatScheduler가 한다.
// 예전의 보스 공격 상태와 스킬 상태를 이 상태 하나가 대신한다.
// 지금 행동이 끝났고(취소 없음, 7-13) 대상(사거리 안의 살아 있는 플레이어)이 없으면 접근 상태로 돌아간다.
public class BossCombatState : IState
{
    private readonly BossMonster boss;

    public BossCombatState(BossMonster boss)
    {
        this.boss = boss;
    }

    public void Enter()
    {
        boss.Combat.Engage();
    }

    public void Execute()
    {
        if (boss.Combat.IsFree && !boss.TryAcquireTarget())
        {
            boss.fsm.ChangeState(new BossRunState(boss));
        }
    }

    public void Exit()
    {
        boss.Combat.Disengage();
    }
}
