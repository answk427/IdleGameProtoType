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

            GameObject monsterObject = PoolManager.Instance.Spawn(monsterPrefab, spawnPosition, Quaternion.identity);

            Monster monster = monsterObject.GetComponent<Monster>();
            if (monster == null)
            {
                monster = monsterObject.AddComponent<Monster>();
            }
            // 반납할 때를 대비해 자신의 원본 프리팹을 기억해둠
            monster.OriginPrefab = monsterPrefab;

            monster.Initialize(stage.MonsterHp, stage.MonsterGoldReward);
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

    // MonsterSpawner.cs 안에 추가
    public void SpawnBoss(StageConfig stage)
    {
        ClearEncounter(); // 혹시 남아있는 일반 몬스터가 있다면 싹 청소

        if (stage.BossPrefab == null) return;

        // 보스는 플레이어 앞쪽 정해진 위치에 1마리만 스폰
        Vector3 spawnPosition = firstMonsterPosition + (spawnAnchor != null ? spawnAnchor.position : Vector3.zero);

        // PoolManager를 쓴다고 가정 (안 쓰면 Instantiate)
        GameObject obj = PoolManager.Instance.Spawn(stage.BossPrefab, spawnPosition, Quaternion.identity, spawnRoot);

        Monster boss = obj.GetComponent<Monster>();
        if (boss == null) boss = obj.AddComponent<Monster>();

        boss.OriginPrefab = stage.BossPrefab;

        // 보스 전용 체력과 보상으로 초기화 (StageConfig에 보스용 스탯이 있어야 함)
        boss.Initialize(stage.BossHp, stage.BossGoldReward);

        activeMonsters.Add(boss);
    }
}
