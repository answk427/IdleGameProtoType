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

        GameManager.Instance.OnScrollStateChanged += HandleScrollChanged;

        HandleScrollChanged(GameManager.Instance.IsScrolling);
    }

    public void Execute()
    {
        
    }

    public void Exit()
    {
        // 상태를 빠져나갈 땐 무조건 구독 취소(중복 실행 방지)
        if (GameManager.Instance != null)
            GameManager.Instance.OnScrollStateChanged -= HandleScrollChanged;
    }

    private void HandleScrollChanged(bool isScrolling)
    {
        //배경이 스크롤 될 때 몬스터는 플레이어에게 접근해야 하므로 Run 상태로 변경
        if (isScrolling && monster.IsAlive)
        {
            monster.fsm.ChangeState(new MonsterRunState(monster));
        }
    }
}
