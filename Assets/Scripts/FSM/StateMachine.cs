using UnityEngine;

public class StateMachine
{
    public IState CurrentState { get; private set; }

    // 상태를 교체하는 함수
    public void ChangeState(IState newState)
    {
        CurrentState?.Exit();    // 기존 상태 종료
        CurrentState = newState; // 새 상태로 교체
        CurrentState?.Enter();   // 새 상태 시작
    }

    // 매 프레임 실행
    public void Update()
    {
        CurrentState?.Execute();
    }
}
