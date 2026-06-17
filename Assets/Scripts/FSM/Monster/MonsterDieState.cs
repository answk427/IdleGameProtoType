using UnityEngine;

public class MonsterDieState : IState
{
    private readonly Monster monster;

    public MonsterDieState(Monster monster)
    {
        this.monster = monster;
    }

    public void Enter()
    {
        monster.PlayDieAnimation();
        GlobalCombatEvents.TriggerMonsterDied(monster, monster.GoldReward, monster.transform.position);
    }

    public void Execute()
    {
    }

    public void Exit()
    {
    }
}
