using System;
using System.Collections.Generic;
using UnityEngine;

// 인카운터·보스 스폰과 정리. 스폰한 몬스터의 전투원은 전투 월드(CombatWorld)에 등록하고, 정리할 때 뺀다.
// 대상 찾기와 범위 판정은 여기가 아니라 CombatWorld의 규칙 하나가 한다(docs/COMBAT_ARCHITECTURE.md 8-4).
// 스폰 위치는 대열 앞장(플레이어) 기준의 게임 데이터다(스테이지 표 spawnDistance, monsterSpacing, 9-3).
// 스폰하기 전에 원점을 앞장 근처로 재설정해서 좌표가 커지지 않게 한다(D16).

public class MonsterSpawner : MonoBehaviour
{
    [SerializeField] private GameObject fallbackMonsterPrefab;
    [SerializeField] private Transform spawnRoot;

    private readonly List<Monster> activeMonsters = new List<Monster>();
    private CombatWorld world;

    public IReadOnlyList<Monster> ActiveMonsters => activeMonsters;

    // CombatSimulation.Initialize가 연결한다. 그 전에는 스폰하지 않는다.
    public void Attach(CombatWorld combatWorld) => world = combatWorld;

    public List<Monster> SpawnEncounter(StageData stage)
    {
        ClearEncounter();

        if (stage == null)
        {
            Debug.LogError("MonsterSpawner needs a StageData.");
            return new List<Monster>();
        }

        int monsterId = stage.NormalMonsterId;
        MonsterEntry monsterEntry = GameDatabaseManager.Instance.GetMonster(monsterId);
        if (monsterEntry == null || monsterEntry.data == null)
        {
            Debug.LogError($"MonsterData is Not Found. monsterId:{monsterId}");
            return new List<Monster>();
        }

        MonsterData monsterData = monsterEntry.data;
        GameObject monsterPrefab = monsterEntry.prefab;
        if (monsterPrefab == null)
        {
            Debug.LogError($"MonsterSpawner needs a monster prefab. monsterID:{monsterId}, monsterName:{monsterData.MonsterName}");
            return new List<Monster>();
        }

        if (!TryPrepareSpawn(stage, out float firstX)) return new List<Monster>();
        Vector3 basePosition = new Vector3(firstX, GetGroundY(0f) + monsterEntry.groundOffset, 0f);

        for (int i = 0; i < stage.MonstersPerEncounter; i++)
        {
            Vector3 spawnPosition = basePosition + Vector3.right * stage.MonsterSpacing * i;
            Monster monster = SpawnMonster(monsterData, stage, monsterPrefab, spawnPosition, null, false);
            if (monster != null) activeMonsters.Add(monster);
        }

        return activeMonsters;
    }

    public void ClearEncounter()
    {
        for (int i = activeMonsters.Count - 1; i >= 0; i--)
        {
            Monster monster = activeMonsters[i];
            if (monster == null) continue;

            world?.Remove(monster.Combatant);
            PoolManager.Instance.Despawn(monster.OriginPrefab, monster.gameObject);
        }
        activeMonsters.Clear();
    }

    public bool AllDie()
    {
        for (int i = 0; i < activeMonsters.Count; i++)
        {
            if (activeMonsters[i].IsAlive) return false;
        }
        return true;
    }

    // parent: 일반 몬스터는 부모 없이(예전과 같음), 보스는 spawnRoot 아래에 둔다.
    private Monster SpawnMonster(MonsterData monsterData, StageData stageData, GameObject monsterPrefab, Vector3 spawnPosition, Transform parent, bool isBoss)
    {
        if (world == null)
        {
            Debug.LogError("[MonsterSpawner] 전투 월드가 연결되지 않아 스폰하지 않습니다(CombatSimulation.Initialize 전).");
            return null;
        }

        GameObject monsterObject = PoolManager.Instance.Spawn(monsterPrefab, spawnPosition, Quaternion.identity, parent);
        if (!monsterObject.TryGetComponent(out Monster monster))
        {
            Debug.LogError($"[MonsterSpawner] {monsterPrefab.name} 프리팹에 Monster 컴포넌트가 없습니다.");
            PoolManager.Instance.Despawn(monsterPrefab, monsterObject);
            return null;
        }

        monster.OriginPrefab = monsterPrefab;
        CalcStageMultiplier(stageData, out double hpMult, out double dmgMult, out double goldMult);
        monster.Spawn(monsterData, hpMult, dmgMult, goldMult, isBoss);
        world.Add(monster.Combatant);
        return monster;
    }

    public Monster SpawnBoss(StageData stage)
    {
        ClearEncounter();

        MonsterEntry bossEntry = GameDatabaseManager.Instance.GetMonster(stage.BossMonsterId);
        if (bossEntry == null || bossEntry.data == null)
        {
            Debug.LogError($"BossMonsterData is not found, stageNumber:{stage.StageNumber}");
            return null;
        }

        GameObject bossPrefab = bossEntry.prefab;
        if (bossPrefab == null)
        {
            Debug.LogError($"MonsterSpawner needs a boss prefab. bossID:{stage.BossMonsterId}");
            return null;
        }

        if (!TryPrepareSpawn(stage, out float firstX)) return null;
        Vector3 spawnPosition = new Vector3(firstX, GetGroundY(0f) + bossEntry.groundOffset, 0f);

        // 보스와 일반 몬스터는 같은 Monster다. 차이는 데이터(스킬, 이동 정책, 공격)와 처치 알림용 역할뿐이다.
        Monster boss = SpawnMonster(bossEntry.data, stage, bossPrefab, spawnPosition, spawnRoot, true);
        if (boss != null) activeMonsters.Add(boss);
        return boss;
    }

    // 원점을 대열 앞장 근처로 재설정하고(D16), 첫 몬스터의 피벗 x = 앞장 몸 중심 + spawnDistance(D18)를 돌려준다.
    // 고정 단계 밖(스폰 루프의 WaitForFixedUpdate 뒤)에서 불린다.
    private bool TryPrepareSpawn(StageData stage, out float firstX)
    {
        firstX = 0f;
        Combatant lead = world?.LeadAlly;
        if (lead == null)
        {
            Debug.LogError("[MonsterSpawner] 대열 앞장(플레이어)이 전투 월드에 없어 스폰 위치를 정할 수 없습니다.");
            return false;
        }

        world.RecenterOn(lead);
        firstX = lead.X + stage.SpawnDistance;
        return true;
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
