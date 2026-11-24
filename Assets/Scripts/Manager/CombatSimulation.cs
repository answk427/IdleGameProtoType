using System.Collections.Generic;
using UnityEngine;

// 전투의 유일한 고정 단계(docs/COMBAT_DESIGN.md 3-1, docs/COMBAT_ARCHITECTURE.md 7-11). 씬에 하나만 둔다.
//
// 한 단계는 정해진 국면 순서로 진행한다.
//   ① 지난 화면 프레임이 그려 둔 보간 위치를 지우고 시뮬레이션 위치로 되돌린다
//   ② 전투 사건: CombatScheduler가 교전 중인 모든 전투원의 행동·쿨다운 사건을 단계 안의 정확한 시각 순서로 처리한다
//   ③ 상태 전이와 이동: 각 상태 기계(달리기↔교전 전환, 몬스터 이동). 위치는 단계마다 적분하므로 여기는 단계 단위다
//   ④ 배경
//   ⑤ 보간용 기록, 시계를 다음 단계로
// 전투 코드는 Time.deltaTime을 쓰지 않고 이 시계(SimClock)의 시각과 단계 길이를 쓴다.
//
// 화면 프레임(Update)에서는 움직이는 것(몬스터, 배경)을 직전 두 단계 사이로 보간해 그리기만 한다.
// 각 객체가 직접 FixedUpdate를 쓰지 않는 이유: 위 순서(특히 ①과 ⑤가 모든 전투 로직의 앞뒤에 오는 것)를 한 곳에서 보이게 하려고.
public class CombatSimulation : MonoBehaviour
{
    [SerializeField] private MonsterSpawner monsterSpawner;
    [SerializeField] private BackgroundScroller backgroundScroller;

    private readonly CombatScheduler scheduler = new CombatScheduler();
    private readonly List<CombatActions> participants = new List<CombatActions>();
    private PlayerController player;

    public SimClock Clock { get; private set; }

    // GameInitializer가 GameManager.Initialize 다음에 부른다. 그 전에는 아무것도 돌지 않는다.
    public void Initialize(PlayerController player)
    {
        this.player = player;
        Clock = new SimClock(Time.fixedDeltaTime);
        player.AttachClock(Clock);
    }

    public bool ValidateReferences()
    {
        bool ok = true;
        if (monsterSpawner == null) { Debug.LogError("[CombatSimulation] monsterSpawner가 연결되지 않았습니다."); ok = false; }
        if (backgroundScroller == null) { Debug.LogError("[CombatSimulation] backgroundScroller가 연결되지 않았습니다."); ok = false; }
        return ok;
    }

    private void FixedUpdate()
    {
        if (player == null) return;

        IReadOnlyList<Monster> monsters = monsterSpawner.ActiveMonsters;

        // ① 보간 위치를 지우고 시뮬레이션 위치로 되돌린다.
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] != null) monsters[i].RestoreSimulatedPose();
        }

        // ② 전투 사건. 같은 시각의 사건은 이 목록 순서(플레이어 → 몬스터 생성 순)로 처리한다.
        participants.Clear();
        if (player.Combat.IsActive) participants.Add(player.Combat);
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] is ICombatParticipant participant && participant.Combat.IsActive) participants.Add(participant.Combat);
        }
        scheduler.RunStep(Clock, participants);

        // ③ 상태 전이와 이동(스폰 목록은 단계 안에서 바뀌지 않는다: 스폰과 정리는 단계 밖에서 한다)
        player.Tick(Clock);
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] != null) monsters[i].Tick(Clock);
        }

        // ④ 배경
        backgroundScroller.Tick(Clock);

        // ⑤ 이번 단계 결과를 보간용으로 기록하고 시계를 넘긴다.
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] != null) monsters[i].CaptureSimulatedPose();
        }
        Clock.AdvanceStep();
    }

    private void Update()
    {
        if (player == null) return;

        // 마지막 고정 단계 이후 흐른 시간의 비율. 그려지는 위치는 실제 시뮬레이션보다 최대 한 단계 늦다.
        float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);

        IReadOnlyList<Monster> monsters = monsterSpawner.ActiveMonsters;
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] != null) monsters[i].ApplyRenderPose(alpha);
        }
        backgroundScroller.ApplyRenderOffset(alpha);
    }
}
