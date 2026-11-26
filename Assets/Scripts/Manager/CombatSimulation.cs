using System;
using System.Globalization;
using UnityEngine;

// 전투 시뮬레이션의 Unity 쪽 드라이버(docs/COMBAT_DESIGN.md 3-1, docs/COMBAT_ARCHITECTURE.md 7-11, 8-3, 9-3, 10-3, 11절). 씬에 하나만 둔다.
// 전투 규칙과 상태는 CombatWorld(순수 C#)가, 흐름(인카운터·보스·스테이지)은 EncounterDirector(순수 C#)가, 고정 단계를 몇 번 어떤
// 순서로 돌릴지는 SimulationLoop(순수 C#, 5-B)가 갖는다. 이 클래스는 화면 프레임마다 흐른 게임 시간을 루프에 넘기고, 그 결과를 그린다.
//
// 한 단계는 정해진 국면 순서로 진행한다(SimulationLoop.Step).
//   ① 흐름: 받아 둔 명령, 지난 단계의 결과(처치, 전멸), 끝난 대기, 스폰·정리(EncounterDirector, D22)
//   ② 전투 사건: 교전 중인 모든 전투원의 행동·쿨다운 사건을 단계 안의 정확한 시각 순서로(CombatScheduler)
//   ③ 단계 전이와 이동: 아군 → 적, 각 팀은 등록 순(이동↔교전 전환, 전진·접근 이동(sweep), 사망 동작)
//   ⑤ 보간용 기록, 시계를 다음 단계로
//   ⑥ 사건 배달: 이번 단계에 쌓인 CombatEvent를 순서대로 뷰·이펙트·보상에 보낸다(5-A). 처리가 다 끝난 뒤라 재진입이 없다.
// (④ 배경은 3단계에서 없어졌다. 배경은 카메라를 따라 화면 프레임에서 그린다.)
// 전투 코드는 Time.deltaTime을 쓰지 않고 시계(SimClock)의 시각과 단계 길이를 쓴다. 흐름의 대기와 스테이지 전환도 이 시계다.
// Time.deltaTime을 읽는 곳은 여기 한 곳이고, 루프의 입력일 뿐이다(예전에는 Unity의 FixedUpdate가 같은 일을 했다).
//
// 다른 Update보다 먼저 돈다(예전 FixedUpdate가 Update보다 먼저였던 순서). 그 뒤 전투원 위치를 직전 두 단계 사이로 보간해 그린다.
// 카메라와 배경은 그 뒤(CameraRig.LateUpdate). 배속(개발자 도구)은 Time.timeScale이라 Time.deltaTime에 들어 있다.
[DefaultExecutionOrder(-500)]
public class CombatSimulation : MonoBehaviour
{
    [SerializeField] private MonsterSpawner monsterSpawner;

    private IDisposable stalledSubscription;

    public SimClock Clock { get; private set; }
    public CombatWorld World { get; private set; }
    public EncounterDirector Director { get; private set; }
    public SimulationLoop Loop { get; private set; }

    // 전투원 → 뷰(사건 배달, 보간, 카메라, 이펙트 위치).
    public CombatantViews Views { get; private set; }

    // GameInitializer가 부른다. 플레이어가 먼저 등록된다(같은 팀 안의 처리 순서). 몬스터는 디렉터가 스폰할 때 등록한다.
    // 흐름은 GameInitializer가 모든 구독을 연결한 뒤 Director.Begin으로 시작한다. 그 전에는 ①이 아무것도 하지 않는다.
    public void Initialize(PlayerController player, Func<int, StageData> findStage, FlowTimings timings)
    {
        Clock = new SimClock(StepLengthOf(Time.fixedDeltaTime));
        World = new CombatWorld(Clock);
        Views = new CombatantViews(World.Events);
        monsterSpawner.Initialize(Views);

        Views.Register(player);
        World.Add(player.Combatant);

        Director = new EncounterDirector(World, monsterSpawner, findStage, timings);
        stalledSubscription = Director.Stalled.Subscribe(message => Debug.LogWarning(message));

        // 한 프레임 상한은 프로젝트 설정(Maximum Allowed Timestep, 지금 1/3초)을 따른다.
        Loop = new SimulationLoop(Clock, World, Director, Time.maximumDeltaTime);
    }

    // 고정 단계의 길이(초). 프로젝트 설정(Fixed Timestep)을 쓴다. Unity 6은 고정 단계를 유리수로 저장해서, 설정값 0.02가
    // fixedDeltaTime(float)으로는 0.0199999921로 들어온다. 그대로 쓰면 데이터의 초가 단계 수와 맞지 않는다(흐름 대기 0.5초 =
    // 25.00001단계 → 26단계, 플레이 측정에서 찾음). 시뮬레이션 시계는 설정이 뜻하는 값(유효숫자 6자리로 반올림)을 쓴다
    // (docs/COMBAT_ARCHITECTURE.md 10-9).
    public static double StepLengthOf(float fixedDeltaTime) =>
        double.Parse(fixedDeltaTime.ToString("G6", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private void OnDestroy()
    {
        stalledSubscription?.Dispose();
        Views?.Dispose();
    }

    public bool ValidateReferences()
    {
        bool ok = true;
        if (monsterSpawner == null) { Debug.LogError("[CombatSimulation] monsterSpawner가 연결되지 않았습니다."); ok = false; }
        return ok;
    }

    private void Update()
    {
        if (Loop == null) return;

        // 흐른 게임 시간만큼 고정 단계를 돌린다(배속과 한 프레임 상한이 들어 있다). 단계 밖의 사건도 여기서 배달된다.
        Loop.Advance(Time.deltaTime);

        // 마지막 단계 이후 흐른 시간의 비율. 그려지는 위치는 실제 시뮬레이션보다 최대 한 단계 늦다.
        float alpha = Loop.Alpha;
        var views = Views.All;
        for (int i = 0; i < views.Count; i++)
        {
            if (views[i] != null && views[i].isActiveAndEnabled) views[i].ApplyRenderPose(alpha); // 풀에 들어간 몬스터는 건너뛴다
        }
    }
}
