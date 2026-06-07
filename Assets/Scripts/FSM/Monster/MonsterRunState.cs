using UnityEngine;

public class MonsterRunState : IState
{
    private Monster monster;

    public MonsterRunState(Monster monster)
    {
        this.monster = monster;
    }

    public void Enter()
    {
        GameManager.Instance.OnScrollStateChanged += HandleScrollChanged;
    }

    public void Execute()
    {
        monster.moveToPlayer();
    }

    public void Exit()
    {
        throw new System.NotImplementedException();
    }

    private void HandleScrollChanged(bool isScrolling)
    {
        //배경이 스크롤 안될 때 몬스터는 대기해야 하므로 Idle 상태로 변경
        if (!isScrolling && monster.IsAlive)
        {
            monster.fsm.ChangeState(new MonsterIdleState(monster));
        }
    }
}
