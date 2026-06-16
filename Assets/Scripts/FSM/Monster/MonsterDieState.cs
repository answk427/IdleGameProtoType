using System.Collections;
using UnityEngine;

public class MonsterDieState : IState
{
    private Monster monster;

    public MonsterDieState(Monster monster)
    {
        this.monster = monster;
    }

    public void Enter()
    {
        monster.PlayDeathAnimation();
    }

    public void Execute()
    {
    }

    public void Exit()
    {
    }
}
