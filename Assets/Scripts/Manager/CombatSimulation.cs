using System;
using System.Globalization;
using UnityEngine;

// 전투의 유일한 고정 단계(docs/COMBAT_DESIGN.md 3-1, docs/COMBAT_ARCHITECTURE.md 7-11, 8-3, 9-3, 10-3). 씬에 하나만 둔다.
// 전투 규칙과 상태는 CombatWorld(순수 C#)가, 흐름(인카운터·보스·스테이지)은 EncounterDirector(순수 C#)가 갖고,
// 이 클래스는 그것을 Unity의 시간에 맞춰 돌리는 드라이버다.
//
// 한 단계는 정해진 국면 순서로 진행한다.
//   ① 흐름: 받아 둔 명령, 지난 단계의 결과(처치, 전멸), 끝난 대기, 스폰·정리(EncounterDirector, D22)
//   ② 전투 사건: 교전 중인 모든 전투원의 행동·쿨다운 사건을 단계 안의 정확한 시각 순서로(CombatScheduler)
//   ③ 단계 전이와 이동: 아군 → 적, 각 팀은 등록 순(이동↔교전 전환, 전진·접근 이동(sweep), 사망 동작)
//   ⑤ 보간용 기록, 시계를 다음 단계로
// (④ 배경은 3단계에서 없어졌다. 배경은 카메라를 따라 화면 프레임에서 그린다.)
// 전투 코드는 Time.deltaTime을 쓰지 않고 이 시계(SimClock)의 시각과 단계 길이를 쓴다. 흐름의 대기와 스테이지 전환도 이 시계다.
//
// 화면 프레임(Update)에서는 전투원 위치를 직전 두 단계 사이로 보간해 그리기만 한다. 카메라와 배경은 그 뒤(CameraRig.LateUpdate).
// 원점 재설정(스폰 때)이 일어나면 살아 있는 연출(동전, 이펙트, 데미지 숫자)을 같은 양만큼 옮기도록 CombatEffectManager에 전한다.
public class CombatSimulation : MonoBehaviour
{
    [SerializeField] private MonsterSpawner monsterSpawner;

    private IDisposable stalledSubscription;

    public SimClock Clock { get; private set; }
    public CombatWorld World { get; private set; }
    public EncounterDirector Director { get; private set; }

    // GameInitializer가 부른다. 플레이어가 먼저 등록된다(같은 팀 안의 처리 순서). 몬스터는 디렉터가 스폰할 때 등록한다.
    // 흐름은 GameInitializer가 모든 구독을 연결한 뒤 Director.Begin으로 시작한다. 그 전에는 ①이 아무것도 하지 않는다.
    public void Initialize(PlayerController player, Func<int, StageData> findStage, FlowTimings timings)
    {
        Clock = new SimClock(StepLengthOf(Time.fixedDeltaTime));
        World = new CombatWorld(Clock);
        World.OriginShifted += HandleOriginShifted;
        World.Add(player.Combatant);

        Director = new EncounterDirector(World, monsterSpawner, findStage, timings);
        stalledSubscription = Director.Stalled.Subscribe(message => Debug.LogWarning(message));
    }

    // 고정 단계의 길이(초). Unity 6은 고정 단계를 유리수로 저장해서, 설정값 0.02가 fixedDeltaTime(float)으로는
    // 0.0199999921로 들어온다. 그대로 쓰면 데이터의 초가 단계 수와 맞지 않는다(흐름 대기 0.5초 = 25.00001단계 → 26단계,
    // 플레이 측정에서 찾음). 시뮬레이션 시계는 설정이 뜻하는 값(유효숫자 6자리로 반올림)을 쓴다. 사람이 적는 설정값은
    // 이 안에 들어간다. Unity가 FixedUpdate를 부르는 간격과의 차이는 단계당 0.00000001초라 화면에는 상관없다
    // (docs/COMBAT_ARCHITECTURE.md 10-9).
    public static double StepLengthOf(float fixedDeltaTime) =>
        double.Parse(fixedDeltaTime.ToString("G6", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private void OnDestroy()
    {
        if (World != null) World.OriginShifted -= HandleOriginShifted;
        stalledSubscription?.Dispose();
    }

    public bool ValidateReferences()
    {
        bool ok = true;
        if (monsterSpawner == null) { Debug.LogError("[CombatSimulation] monsterSpawner가 연결되지 않았습니다."); ok = false; }
        return ok;
    }

    private void FixedUpdate()
    {
        if (World == null) return;

        // ① 흐름
        Director.Tick();

        // ② 전투 사건
        World.RunEvents();

        // ③ 단계 전이와 이동
        World.StepPhases();

        // ⑤ 이번 단계 결과를 보간용으로 기록하고 시계를 넘긴다.
        World.CaptureStep();
        Clock.AdvanceStep();
    }

    private void Update()
    {
        if (World == null) return;

        // 마지막 고정 단계 이후 흐른 시간의 비율. 그려지는 위치는 실제 시뮬레이션보다 최대 한 단계 늦다.
        float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);

        var combatants = World.Combatants;
        for (int i = 0; i < combatants.Count; i++)
        {
            if (combatants[i].Host is CombatantHost host) host.ApplyRenderPose(alpha);
        }
    }

    private static void HandleOriginShifted(float delta)
    {
        if (CombatEffectManager.Instance != null) CombatEffectManager.Instance.ShiftOrigin(delta);
    }
}
