using System.Collections.Generic;
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [SerializeField] private GameObject fallbackMonsterPrefab;
    [SerializeField] private Transform spawnAnchor;
    [SerializeField] private Transform spawnRoot;
    [SerializeField] private Vector3 firstMonsterPosition = new Vector3(0f, 0f, 0f);

    private readonly List<Monster> activeMonsters = new List<Monster>();

    public IReadOnlyList<Monster> ActiveMonsters => activeMonsters;

    public List<Monster> SpawnEncounter(StageData stage)
    {
        ClearEncounter();

        if (stage == null)
        {
            Debug.LogError("MonsterSpawner needs a StageData.");
            return new List<Monster>();
        }

        int monsterId = stage.normalMonsterId;
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
            Debug.LogError($"MonsterSpawner needs a monster prefab. monsterID:{monsterId}, monsterName:{monsterData.monsterName}");
            return new List<Monster>();
        }

        Vector3 basePosition = firstMonsterPosition;
        if (spawnAnchor != null)
            basePosition += spawnAnchor.position;

        for (int i = 0; i < stage.monstersPerEncounter; i++)
        {
            Vector3 spawnPosition = basePosition + Vector3.right * stage.monsterSpacing * i;
            Monster monster = SpawnMonster(monsterData, stage, monsterPrefab, spawnPosition, Quaternion.identity);
            activeMonsters.Add(monster);
        }

        return activeMonsters;
    }

    public void ClearEncounter()
    {
        for (int i = activeMonsters.Count - 1; i >= 0; i--)
        {
            Monster monster = activeMonsters[i];
            if (monster != null)
                PoolManager.Instance.Despawn(monster.OriginPrefab, monster.gameObject);
        }
        activeMonsters.Clear();
    }

    public Monster GetClosestMonster(float originX)
    {
        Monster closestMonster = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < activeMonsters.Count; i++)
        {
            Monster m = activeMonsters[i];
            if (m == null || !m.IsAlive) continue;

            float distance = m.transform.position.x - originX;
            if (distance > 0 && distance < minDistance)
            {
                minDistance = distance;
                closestMonster = m;
            }
        }
        return closestMonster;
    }

    public List<Monster> GetMonstersInRange(float originX, float radius)
    {
        List<Monster> result = new List<Monster>();
        for (int i = 0; i < activeMonsters.Count; i++)
        {
            Monster m = activeMonsters[i];
            if (m == null || !m.IsAlive) continue;

            float distance = Mathf.Abs(m.transform.position.x - originX);
            if (distance <= radius)
            {
                result.Add(m);
            }
        }
        return result;
    }

    public bool AllDie()
    {
        if (activeMonsters.Count == 0) return true;

        for (int i = 0; i < activeMonsters.Count; i++)
        {
            if (activeMonsters[i].IsAlive) return false;
        }
        return true;
    }

    public Monster SpawnMonster(MonsterData monsterData, StageData stageData, GameObject monsterPrefab, Vector3 spawnPosition, Quaternion quaternion)
    {
        GameObject monsterObject = PoolManager.Instance.Spawn(monsterPrefab, spawnPosition, quaternion);

        Monster monster = monsterObject.GetComponent<Monster>();
        if (monster == null)
            monster = monsterObject.AddComponent<Monster>();

        monster.OriginPrefab = monsterPrefab;

        CalcStageMultiplier(stageData, out float hpMult, out float dmgMult, out float goldMult);
        monster.Initialize(monsterData, goldMult, hpMult, dmgMult);

        return monster;
    }

    public BossMonster SpawnBoss(StageData stage)
    {
        ClearEncounter();

        MonsterEntry bossEntry = GameDatabaseManager.Instance.GetMonster(stage.bossMonsterId);
        if (bossEntry == null || bossEntry.data == null)
        {
            Debug.LogError($"BossMonsterData is not found, stageNumber:{stage.stageNumber}");
            return null;
        }

        MonsterData monsterData = bossEntry.data;
        GameObject bossPrefab = bossEntry.prefab;
        if (bossPrefab == null)
        {
            Debug.LogError($"MonsterSpawner needs a boss prefab. bossID:{stage.bossMonsterId}");
            return null;
        }

        Vector3 spawnPosition = firstMonsterPosition + (spawnAnchor != null ? spawnAnchor.position : Vector3.zero);
        GameObject obj = PoolManager.Instance.Spawn(bossPrefab, spawnPosition, Quaternion.identity, spawnRoot);

        // 보스 프리팹엔 BossMonster 컴포넌트가 붙어있어야 함
        BossMonster boss = obj.GetComponent<BossMonster>();
        if (boss == null)
        {
            Debug.LogError($"{bossPrefab.name} prefab에 BossMonster 컴포넌트가 없습니다.");
            PoolManager.Instance.Despawn(bossPrefab, obj);
            return null;
        }

        boss.OriginPrefab = bossPrefab;

        CalcStageMultiplier(stage, out float hpMult, out float dmgMult, out float goldMult);
        boss.Initialize(monsterData, goldMult, hpMult, dmgMult);

        activeMonsters.Add(boss);

        return boss;
    }

    private static void CalcStageMultiplier(StageData stageData, out float hpMult, out float dmgMult, out float goldMult)
    {
        hpMult = Mathf.Pow(1.15f, stageData.stageNumber);
        dmgMult = Mathf.Pow(1.10f, stageData.stageNumber);
        goldMult = Mathf.Pow(1.05f, stageData.stageNumber);
    }
}
