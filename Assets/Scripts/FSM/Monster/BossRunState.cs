public class BossRunState : MonsterRunState
{
    protected BossMonster boss;

    public BossRunState(BossMonster boss) : base(boss)
    {
        this.boss = boss;
    }

    public override void Enter()
    {
    }

    public override void Execute()
    {
        base.Execute();

        // 플레이어가 사거리 안에 들어오면 공격 상태로 전환
        PlayerController player = GameManager.Instance.GetPlayer();
        if (player == null) return;

        // 중심점 사이 거리에서 각자의 반너비를 빼서, 보스가 덩치가 커도
        // 플레이어 몸 속으로 들어가지 않고 그 몫만큼 미리 멈춰서 공격하게 한다.
        float edgeGap = (boss.transform.position.x - player.transform.position.x) - boss.HalfWidth - player.HalfWidth;
        if (edgeGap <= boss.AttackRange)
        {
            boss.fsm.ChangeState(new BossAttackState(boss, player));
        }
    }

    public override void Exit()
    {
    }
}
