// 보스의 접근 상태. 플레이어 쪽으로 걷다가(CCD로 몸 앞에서 멈춤) 대상이 사거리 안에 들어오면 교전한다.
// 대상 판정은 교전 상태와 같은 것(BossMonster.TryAcquireTarget)을 써서, 두 상태가 서로 다른 기준으로 오가며 떨지 않게 한다.
public class BossRunState : MonsterRunState
{
    protected BossMonster boss;

    public BossRunState(BossMonster boss) : base(boss)
    {
        this.boss = boss;
    }

    public override void Enter()
    {
        boss.PlayIdleAnimation();
    }

    public override void Execute()
    {
        base.Execute();

        if (boss.TryAcquireTarget())
        {
            boss.fsm.ChangeState(new BossCombatState(boss));
        }
    }

    public override void Exit()
    {
    }
}
