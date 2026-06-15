using System.Collections.Generic;
using UnityEditor.SceneManagement;
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
            Debug.LogError($"MonsterSpawner needs a StageData. stageNumber:{stage.stageNumber}");
            return new List<Monster>();
        }

        int monsterId = stage.normalMonsterId;
        if (!DataManager.Instance.MonsterDict.TryGetValue(monsterId, out MonsterData monsterData))
        {
            Debug.LogError($"MonsterStatData is Not Found. monsterId:{monsterId}");
            return new List<Monster>();
        }

        GameObject monsterPrefab = GetMonsterPrefab(monsterData.monsterName);
        if (monsterPrefab == null)
        {
            Debug.LogError($"MonsterSpawner needs a monster prefab. monsterID:{monsterId}, monsterName:{monsterData.monsterName}");
            return new List<Monster>();
        }

        Vector3 basePosition = firstMonsterPosition;
        if (spawnAnchor != null)
        {
            basePosition += spawnAnchor.position;
        }

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
            {
                PoolManager.Instance.Despawn(monster.OriginPrefab, monster.gameObject);
            }
        }

        activeMonsters.Clear();
    }


    // 기준 위치(originX)를 주면, 그것보다 오른쪽에 있는 가장 가까운 몬스터를 반환
    public Monster GetClosestMonster(float originX)
    {
        Monster closestMonster = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < activeMonsters.Count; i++)
        {
            Monster m = activeMonsters[i];
            if (m == null || !m.IsAlive) continue;

            float distance = m.transform.position.x - originX;

            // 기준점보다 앞에 있고, 최소 거리보다 가깝다면 갱신
            if (distance > 0 && distance < minDistance)
            {
                minDistance = distance;
                closestMonster = m;
            }
        }
        return closestMonster;
    }

    public bool AllDie()
    {
        if (activeMonsters.Count == 0) return true;

        int deadCount = 0;
        for (int i = 0; i < activeMonsters.Count; i++)
        {
            Monster m = activeMonsters[i];
            if (m.IsAlive == false) deadCount++;
        }

        if (deadCount == activeMonsters.Count) return true;

        return false;
    }

    public Monster SpawnMonster(MonsterData monsterData, StageData stageData, GameObject monsterPrefab, Vector3 spawnPosition, Quaternion quaternion)
    {
        Vector3 basePosition = firstMonsterPosition;
        if (spawnAnchor != null)
        {
            basePosition += spawnAnchor.position;
        }


        GameObject monsterObject = PoolManager.Instance.Spawn(monsterPrefab, spawnPosition, quaternion);

        Monster monster = monsterObject.GetComponent<Monster>();
        if (monster == null)
        {
            monster = monsterObject.AddComponent<Monster>();
        }

        // 반납할 때를 대비해 자신의 원본 프리팹을 기억해둠
        monster.OriginPrefab = monsterPrefab;

        float hpMult, dmgMult, goldMult;
        CalcStageMultiplier(stageData, out hpMult, out dmgMult, out goldMult);

        monster.Initialize(monsterData, goldMult, hpMult, dmgMult);

        return monster;
    }

    public Monster SpawnBoss(StageData stage)
    {
        ClearEncounter(); // 혹시 남아있는 일반 몬스터가 있다면 싹 청소
        
        if (!DataManager.Instance.MonsterDict.TryGetValue(stage.bossMonsterId, out MonsterData monsterData))
        {
            Debug.LogError($"BossMonsterData is not found, stageNumber:{stage.stageNumber}");
            return null;
        }

        GameObject bossPrefab = GetMonsterPrefab(monsterData.monsterName);

        if (bossPrefab == null)
        {
            Debug.LogError($"MonsterSpawner needs a monster prefab. bossID:{stage.bossMonsterId}");
            return null;
        }

        // 보스는 플레이어 앞쪽 정해진 위치에 1마리만 스폰
        Vector3 spawnPosition = firstMonsterPosition + (spawnAnchor != null ? spawnAnchor.position : Vector3.zero);

        GameObject obj = PoolManager.Instance.Spawn(bossPrefab, spawnPosition, Quaternion.identity, spawnRoot);

        Monster boss = obj.GetComponent<Monster>();
        if (boss == null) boss = obj.AddComponent<Monster>();

        boss.OriginPrefab = bossPrefab;

        float hpMult, dmgMult, goldMult;
        CalcStageMultiplier(stage, out hpMult, out dmgMult, out goldMult);
        boss.Initialize(monsterData, goldMult, hpMult, dmgMult);

        activeMonsters.Add(boss);

        return boss;
    }

    public GameObject GetMonsterPrefab(string monsterName)
    {
        string monsterPath = $"Prefabs/Monsters/{monsterName}";
        Debug.Log($"monsterPath:{monsterPath}");
        GameObject monsterPrefab = Resources.Load<GameObject>(monsterPath);
        return monsterPrefab;
    }

    private static void CalcStageMultiplier(StageData stageData, out float hpMult, out float dmgMult, out float goldMult)
    {
        hpMult = Mathf.Pow(1.15f, stageData.stageNumber);
        dmgMult = Mathf.Pow(1.10f, stageData.stageNumber);
        goldMult = Mathf.Pow(1.05f, stageData.stageNumber);
    }
}