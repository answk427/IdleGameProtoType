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
        PlayerController player = GameManager.Instance != null ? GameManager.Instance.GetPlayer() : null;
        if (player == null) return;

        if (CombatRangeUtility.IsWithinAttackRange(
                boss.transform.position, boss.HalfWidth, boss.AttackRange,
                player.transform.position, player.HalfWidth))
        {
            boss.fsm.ChangeState(new BossAttackState(boss, player));
        }
    }

    public override void Exit()
    {
    }
}
