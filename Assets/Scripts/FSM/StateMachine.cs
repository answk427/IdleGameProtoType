using UnityEngine;

public class StateMachine
{
    public IState CurrentState { get; private set; }

    // ���¸� ��ü�ϴ� �Լ�
    public void ChangeState(IState newState)
    {
        CurrentState?.Exit();    // ���� ���� ����
        CurrentState = newState; // �� ���·� ��ü
        CurrentState?.Enter();   // �� ���� ����
    }

    // �� ������ ����
    public void Update()
    {
        CurrentState?.Execute();
    }
}
