using System;
using UnityEngine;

// 흐름의 뷰(IEncounterHost, docs/COMBAT_ARCHITECTURE.md 10-3). 무엇을, 몇 마리, 어디에, 언제 스폰할지는
// EncounterDirector가 정하고, 여기는 그 결정을 화면의 객체로 만든다: 풀에서 꺼내 바닥 높이에 세우고 능력치를 넣는다.
// 월드 등록·해제, 스폰 위치, 원점 재설정은 디렉터가 한다(예전에는 여기 있었다).
public class MonsterSpawner : MonoBehaviour, IEncounterHost
{
    [Tooltip("보스를 둘 부모(일반 몬스터는 부모 없이 둔다)")]
    [SerializeField] private Transform spawnRoot;

    public Combatant Spawn(int monsterId, StageData stage, float pivotX, bool isBoss)
    {
        MonsterEntry entry = GameDatabaseManager.Instance.GetMonster(monsterId);
        if (entry == null || entry.data == null)
        {
            Debug.LogError($"[MonsterSpawner] 몬스터 {monsterId}가 MonsterDatabase에 없습니다(스테이지 {stage.StageNumber}).");
            return null;
        }

        GameObject prefab = entry.prefab;
        if (prefab == null)
        {
            Debug.LogError($"[MonsterSpawner] 몬스터 {monsterId}({entry.data.MonsterName})의 프리팹이 없습니다.");
            return null;
        }

        var position = new Vector3(pivotX, GetGroundY(0f) + entry.groundOffset, 0f);
        GameObject instance = PoolManager.Instance.Spawn(prefab, position, Quaternion.identity, isBoss ? spawnRoot : null);
        if (!instance.TryGetComponent(out Monster monster))
        {
            Debug.LogError($"[MonsterSpawner] {prefab.name} 프리팹에 Monster 컴포넌트가 없습니다.");
            PoolManager.Instance.Despawn(prefab, instance);
            return null;
        }

        // 보스와 일반 몬스터는 같은 Monster다. 차이는 데이터(스킬, 이동 정책, 공격)와 처치 알림용 역할뿐이다.
        monster.OriginPrefab = prefab;
        CalcStageMultiplier(stage, out double hpMult, out double dmgMult, out double goldMult);
        monster.Spawn(entry.data, hpMult, dmgMult, goldMult, isBoss);
        return monster.Combatant;
    }

    public void Despawn(Combatant combatant)
    {
        if (combatant?.Host is Monster monster && monster != null)
            PoolManager.Instance.Despawn(monster.OriginPrefab, monster.gameObject);
    }

    // 스테이지를 떠날 때 그 스테이지 몬스터의 풀을 비운다.
    public void ReleaseStage(StageData stage)
    {
        foreach (int monsterId in new[] { stage.NormalMonsterId, stage.BossMonsterId })
        {
            MonsterEntry entry = GameDatabaseManager.Instance.GetMonster(monsterId);
            if (entry?.prefab != null) PoolManager.Instance.ClearPool(entry.prefab);
        }
    }

    // 항상 바닥(GroundY)에 붙어있는 것처럼 보이도록 고정 Y를 반환.
    // PlayAreaBounds가 없으면(테스트 씬 등) 원래 Y를 그대로 사용 - 안전 가드.
    private static float GetGroundY(float fallbackY)
    {
        return PlayAreaBounds.Instance != null ? PlayAreaBounds.Instance.GroundY : fallbackY;
    }

    // 스테이지 성장 배율. double로 계산한다(float 지수는 큰 스테이지에서 정밀도를 잃고, 결과는 GameNumber로 간다).
    private static void CalcStageMultiplier(StageData stageData, out double hpMult, out double dmgMult, out double goldMult)
    {
        BalanceConfig balance = GameDatabaseManager.Instance.Balance;
        hpMult = Math.Pow(balance.hpGrowthBase, stageData.StageNumber);
        dmgMult = Math.Pow(balance.damageGrowthBase, stageData.StageNumber);
        goldMult = Math.Pow(balance.goldGrowthBase, stageData.StageNumber);
    }
}
