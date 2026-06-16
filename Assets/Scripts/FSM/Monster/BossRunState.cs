using UnityEngine;

public class BossRunState : MonsterRunState
{
    protected BossMonster boss;

    public BossRunState(BossMonster boss) : base(boss)
    {
        this.boss = boss;
    }

    public override void Enter()
    {
        Debug.Log("BossRunState Enter()");
    }

    public override void Execute()
    {
        base.Execute();

        // 플레이어가 사거리 안에 들어오면 공격 상태로 전환
        PlayerController player = GameManager.Instance.GetPlayer();
        if (player == null) return;

        float distance = boss.transform.position.x - player.transform.position.x;
        if (distance <= boss.AttackRange)
        {
            boss.fsm.ChangeState(new BossAttackState(boss, player));
        }
    }

    public override void Exit()
    {
        Debug.Log("BossRunState Exit()");
    }
}
