using System.Collections.Generic;
using UnityEngine;

// 전투의 유일한 고정 단계(docs/COMBAT_DESIGN.md 3-1). 씬에 하나만 둔다.
//
// 위치를 읽거나 움직이는 전투 로직과 전투 타이머는 여기서 부르는 Tick 안에서만 돈다.
// 그래서 전투 결과가 화면 프레임률과 무관하다(긴 프레임 한 번은 짧은 단계 여러 번으로 나뉘어 돈다).
// Tick 안의 Time.deltaTime은 Unity가 Time.fixedDeltaTime(0.02초)을 돌려주므로 상태 코드는 Time.deltaTime을 그대로 쓴다.
//
// 화면 프레임(Update)에서는 움직이는 것(몬스터, 배경)을 직전 두 단계 사이로 보간해 그리기만 한다.
// 각 객체가 직접 FixedUpdate를 쓰지 않는 이유: 보간 위치 되돌리기(1)와 기록(5)이
// 모든 전투 로직의 앞뒤에 와야 하는데, 그 순서를 한 곳에서 보이게 하려고.
public class CombatSimulation : MonoBehaviour
{
    [SerializeField] private MonsterSpawner monsterSpawner;
    [SerializeField] private BackgroundScroller backgroundScroller;

    private PlayerController player;

    // GameInitializer가 GameManager.Initialize 다음에 부른다. 그 전에는 아무것도 돌지 않는다.
    public void Initialize(PlayerController player)
    {
        this.player = player;
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

        // 1. 지난 화면 프레임이 그려 둔 보간 위치를 지우고 시뮬레이션 위치로 되돌린다.
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] != null) monsters[i].RestoreSimulatedPose();
        }

        // 2~4. 정해진 순서로 한 단계 진행 (스포너 목록은 단계 안에서 바뀌지 않는다: 스폰과 정리는 단계 밖에서 한다)
        player.Tick();
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] != null) monsters[i].Tick();
        }
        backgroundScroller.Tick();

        // 5. 이번 단계 결과를 보간용으로 기록한다.
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] != null) monsters[i].CaptureSimulatedPose();
        }
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
