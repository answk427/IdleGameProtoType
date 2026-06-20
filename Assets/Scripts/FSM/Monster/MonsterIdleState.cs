using UnityEngine;

public class MonsterIdleState : IState
{
    private Monster monster;

    public MonsterIdleState(Monster monster)
    {
        this.monster = monster;
    }

    public void Enter()
    {
        monster.PlayIdleAnimation();

        GlobalGameEvents.OnScrollChanged += HandleScrollChanged;

        // 현재 스크롤 상태 즉시 반영
        HandleScrollChanged(GameManager.Instance.IsScrolling);
    }

    public void Execute()
    {
    }

    public void Exit()
    {
        GlobalGameEvents.OnScrollChanged -= HandleScrollChanged;
    }

    private void HandleScrollChanged(bool isScrolling)
    {
        if (isScrolling && monster.IsAlive)
        {
            monster.fsm.ChangeState(new MonsterRunState(monster));
        }
    }
}
