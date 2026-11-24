using UnityEngine;

// 사망 상태. 사망 행동(길이 = 사망 클립 길이)이 끝나면 스테이지 재시작을 알린다.
// 예전에는 애니메이션 이벤트를 기다려서, 이벤트가 오지 않으면(클립 교체, 애니메이터 컬링) 영원히 재시작되지 않았다.
// 시간표는 죽은 시각(사건 처리 중의 시계 시각, 단계 중간일 수 있음)부터 진행한다.
// 재시작까지의 시간을 흐름의 상수로 옮기는 것은 4단계(EncounterDirector)에서 한다.
public class PlayerDieState : IState, IActionListener
{
    private readonly PlayerController player;
    private readonly ActionRunner runner = new ActionRunner();
    private bool finished;
    private bool notified;

    public PlayerDieState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        player.PlayDeathAnimation();
        finished = false;
        notified = false;

        double deathTime = player.Clock != null ? player.Clock.Now : 0.0;
        if (player.TryGetTimeline(ActionKeys.Death, out ActionTimeline death) && death.IsValid)
        {
            runner.Start(death, death.ClipLength, deathTime);
        }
        else
        {
            // 진행이 멈추지 않게 바로 재시작한다. 숨기지 않고 에러로 남긴다.
            Debug.LogError("[PlayerDieState] 사망 시간표가 없거나 잘못돼 기다리지 않고 재시작합니다.");
            finished = true;
        }
    }

    // 상태 전이 국면(단계 끝)에 불린다. 시계의 지금 시각까지 시간표를 진행한다.
    public void Execute()
    {
        if (player.Clock != null) runner.FireMarkersUpTo(player.Clock.Now, this);

        // 알림은 표식 처리가 끝난 뒤에 보낸다(받는 쪽이 이 상태를 바로 바꿔도 러너가 처리 중이 아니게).
        if (finished && !notified)
        {
            notified = true;
            GlobalGameEvents.TriggerPlayerDied();
        }
    }

    public void Exit()
    {
        runner.Cancel();
    }

    void IActionListener.OnActionHit(ActionTimeline action, int hitIndex) { }
    void IActionListener.OnActionCompleted(ActionTimeline action) => finished = true;
}
