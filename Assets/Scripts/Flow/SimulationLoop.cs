using System;

// 전투 시뮬레이션의 루프(docs/COMBAT_ARCHITECTURE.md 11절 5-B). 순수 C#이라 씬 없이 돈다(R7).
//
// - 화면 프레임 하나에 흐른 게임 시간을 받아(Advance) 고정 단계를 필요한 만큼 돌리고, 남은 시간의 비율(보간)을 돌려준다.
//   결과는 단계의 수와 순서로만 정해진다: 프레임 길이가 어떻게 나뉘어 들어와도 같은 단계에서 같은 일이 일어난다.
// - 한 단계는 국면 순서 그대로다: ① 흐름 → ② 전투 사건 → ③ 단계 전이와 이동 → ⑤ 보간 기록·시계 → ⑥ 사건 배달.
// - 한 프레임에 받는 시간에는 상한이 있다(MaxFrameSeconds, Unity의 Maximum Allowed Timestep과 같은 뜻). 아주 긴 프레임
//   (앱이 내려갔다 올라옴, 디버거 정지)에 수백 단계를 한꺼번에 돌다가 다음 프레임이 또 길어지는 일을 막는다. 상한을 넘은 시간은 버린다.
// - 배속은 입력 시간에 이미 들어 있다(Unity 쪽은 Time.timeScale이 반영된 Time.deltaTime을 넘긴다). 그래서 애니메이터·파티클처럼
//   게임 시간으로 도는 연출이 같이 빨라지고, 화면 시간(unscaled)을 쓰는 UI는 영향을 받지 않는다(D6, UI_DESIGN 6-5).
// - 단계 밖에서 생긴 사건(능력치 변경 등)도 프레임마다 배달한다.
public sealed class SimulationLoop
{
    public const double DefaultMaxFrameSeconds = 1.0 / 3.0;

    private readonly SimClock clock;
    private readonly CombatWorld world;
    private readonly EncounterDirector director;
    private double accumulator;

    // director는 없어도 된다(전투만 돌리는 시험).
    public SimulationLoop(SimClock clock, CombatWorld world, EncounterDirector director, double maxFrameSeconds = DefaultMaxFrameSeconds)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.director = director;
        if (!(maxFrameSeconds > 0.0) || double.IsInfinity(maxFrameSeconds))
            throw new ArgumentOutOfRangeException(nameof(maxFrameSeconds), maxFrameSeconds, "한 프레임 상한은 0보다 큰 유한한 값이어야 합니다.");
        MaxFrameSeconds = maxFrameSeconds;
    }

    public double MaxFrameSeconds { get; }

    // 아직 단계가 되지 않은 시간(초). 0 이상, 단계 길이 미만.
    public double Pending => accumulator;

    // 마지막 단계 이후 흐른 시간의 비율(0 이상 1 미만). 화면 보간에 쓴다.
    public float Alpha => (float)(accumulator / clock.StepLength);

    // 화면 프레임 하나. 흐른 게임 시간(초)만큼 단계를 돌리고, 돌린 단계 수를 돌려준다. 음수·NaN은 0으로 본다.
    public int Advance(double frameSeconds)
    {
        if (!(frameSeconds > 0.0)) frameSeconds = 0.0;
        accumulator += Math.Min(frameSeconds, MaxFrameSeconds);

        int steps = 0;
        double length = clock.StepLength;
        while (accumulator >= length)
        {
            accumulator -= length;
            Step();
            steps++;
        }

        world.DeliverEvents(); // 단계 밖의 사건
        return steps;
    }

    // 고정 단계 하나(국면 순서).
    public void Step()
    {
        // ① 흐름: 받아 둔 명령, 지난 단계의 결과(처치, 전멸), 끝난 대기, 스폰·정리
        director?.Tick();

        // ② 전투 사건: 교전 중인 전투원의 행동·쿨다운 사건을 단계 안의 정확한 시각 순서로
        world.RunEvents();

        // ③ 단계 전이와 이동: 아군 → 적, 각 팀은 등록 순
        world.StepPhases();

        // ⑤ 이번 단계 결과를 보간용으로 기록하고 시계를 넘긴다.
        world.CaptureStep();
        clock.AdvanceStep();

        // ⑥ 사건 배달. 단계마다 한다(풀에서 다시 꺼낸 몬스터는 같은 전투원이라, 지난 사건이 재스폰 뒤에 오면 안 된다).
        world.DeliverEvents();
    }
}
