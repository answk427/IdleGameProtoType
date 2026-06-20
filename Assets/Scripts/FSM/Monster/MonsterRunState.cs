using UnityEngine;

public class MonsterRunState : IState
{
    protected Monster monster;

    public MonsterRunState(Monster monster)
    {
        this.monster = monster;
    }

    public virtual void Enter()
    {
        Debug.Log("MonsterRunState Enter()");
        GlobalGameEvents.OnScrollChanged += HandleScrollChanged;
    }

    public virtual void Execute()
    {
        monster.moveToPlayer();
    }

    public virtual void Exit()
    {
        GlobalGameEvents.OnScrollChanged -= HandleScrollChanged;
    }

    private void HandleScrollChanged(bool isScrolling)
    {
        if (!isScrolling && monster.IsAlive)
        {
            monster.fsm.ChangeState(new MonsterIdleState(monster));
        }
    }
}
