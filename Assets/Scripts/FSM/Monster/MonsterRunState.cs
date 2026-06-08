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
        Debug.Log("MonsterRunState Enter()");
        GameManager.Instance.OnScrollStateChanged += HandleScrollChanged;
    }

    public void Execute()
    {
        monster.moveToPlayer();
    }

    public void Exit()
    {
        // 상태를 빠져나갈 땐 무조건 구독 취소(중복 실행 방지)
        if (GameManager.Instance != null)
            GameManager.Instance.OnScrollStateChanged -= HandleScrollChanged;
    }

    private void HandleScrollChanged(bool isScrolling)
    {
        Debug.Log($"MonsterRunState.HandleScrollChanged, isScrolling : {isScrolling}");

        //배경이 스크롤 안될 때 몬스터는 대기해야 하므로 Idle 상태로 변경
        if (!isScrolling && monster.IsAlive)
        {
            Debug.Log("몬스터 Idle 상태로 변경");
            monster.fsm.ChangeState(new MonsterIdleState(monster));
        }
    }
}
