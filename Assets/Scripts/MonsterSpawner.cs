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

    public List<Monster> SpawnEncounter(StageConfig stage)
    {
        ClearEncounter();

        if (stage == null)
        {
            Debug.LogError("MonsterSpawner needs a StageConfig.");
            return new List<Monster>();
        }

        GameObject monsterPrefab = stage.MonsterPrefab != null ? stage.MonsterPrefab : fallbackMonsterPrefab;
        if (monsterPrefab == null)
        {
            Debug.LogError("MonsterSpawner needs a monster prefab from StageConfig or fallback.");
            return new List<Monster>();
        }

        Vector3 basePosition = firstMonsterPosition;
        if (spawnAnchor != null)
        {
            basePosition += spawnAnchor.position;
        }

        for (int i = 0; i < stage.MonstersPerEncounter; i++)
        {
            Vector3 spawnPosition = basePosition + Vector3.right * stage.MonsterSpacing * i;
            GameObject monsterObject = Instantiate(monsterPrefab, spawnPosition, Quaternion.identity, spawnRoot);

            Monster monster = monsterObject.GetComponent<Monster>();
            if (monster == null)
            {
                monster = monsterObject.AddComponent<Monster>();
            }

            monster.Initialize(stage.MonsterHp, stage.MonsterGoldReward);
            activeMonsters.Add(monster);
        }

        return new List<Monster>(activeMonsters);
    }

    public void ClearEncounter()
    {
        for (int i = activeMonsters.Count - 1; i >= 0; i--)
        {
            Monster monster = activeMonsters[i];

            if (monster != null)
            {
                Destroy(monster.gameObject);
            }
        }

        activeMonsters.Clear();
    }

    public List<Monster> GetActiveMonsters()
    {
        return activeMonsters;
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

    //임시 함수(삭제 필수)
    public bool AllDie()
    {
        if (activeMonsters.Count == 0) return true;

        int deadCount = 0;
        for(int i = 0;i < activeMonsters.Count;i++)
        {
            Monster m = activeMonsters[i];
            if (m.IsAlive == false) deadCount++;
        }

        if (deadCount == activeMonsters.Count) return true;

        return false;
    }
}
