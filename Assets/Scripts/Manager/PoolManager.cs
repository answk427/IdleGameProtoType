using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance;

    // 프리팹을 키값으로, 해당 프리팹의 전용 '유니티 풀'을 값으로 가지는 딕셔너리
    private Dictionary<GameObject, IObjectPool<GameObject>> poolDict = new Dictionary<GameObject, IObjectPool<GameObject>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    //해당 프리팹 전용 Pool이 없으면 만들고, 있으면 가져옴
    private IObjectPool<GameObject> GetPool(GameObject prefab)
    {
        if (!poolDict.ContainsKey(prefab))
        {
            return null;
        }

        return poolDict[prefab];
    }

    private IObjectPool<GameObject> CreatePool(GameObject prefab)
    {
        if (poolDict.ContainsKey(prefab))
        {
            Debug.LogError($"[PoolManager] 이미 존재하는 풀입니다: {prefab.name}");
            return poolDict[prefab];
        }
        // 유니티 내장 풀 4대 지침 세팅
        IObjectPool<GameObject> newPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(prefab),             // 1. 없을 땐 Instantiate
            actionOnGet: (obj) => obj.SetActive(true),         // 2. 꺼낼 땐 켜기
            actionOnRelease: (obj) => obj.SetActive(false),    // 3. 반납할 땐 끄기
            actionOnDestroy: (obj) => Destroy(obj),            // 4. 용량 꽉 차면 진짜 파괴
            collectionCheck: true,
            defaultCapacity: 10,
            maxSize: 100
        );
        poolDict.Add(prefab, newPool);
        
        return poolDict[prefab];
    }


    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
    {
        IObjectPool<GameObject> pool = GetPool(prefab);
        if (pool == null)
        {
            pool = CreatePool(prefab);
        }

        GameObject obj = pool.Get();

        obj.transform.position = position;
        obj.transform.rotation = rotation;

        // UI 오브젝트 등 캔버스(부모) 설정이 필요할 때를 위한 옵션
        if (parent != null)
        {
            obj.transform.SetParent(parent, worldPositionStays);

            if (obj.transform is RectTransform rect)
            {
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }
        }

        return obj;
    }

    public void Despawn(GameObject prefab, GameObject obj)
    {
        if (prefab == null || obj == null) return;

        IObjectPool<GameObject> pool = GetPool(prefab);
        if (pool != null)
        {
            pool.Release(obj);
        }
    }

    // 특정 프리팹 Pool 삭제
    public void ClearPool(GameObject prefab)
    {
        if (poolDict.ContainsKey(prefab))
        {
            poolDict[prefab].Clear();
            poolDict.Remove(prefab);
        }
    }

    public void ClearAllPools()
    {
        foreach (var pool in poolDict.Values)
        {
            pool.Clear();
        }
        poolDict.Clear();
    }
}