using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

// 현재 게임플레이 경로에서는 사용되지 않음 (GameDatabaseManager의 ScriptableObject
// 파이프라인이 실제 데이터 소스). Resources/Data의 JSON을 직접 읽어와야 하는 상황을
// 위해 남겨둔 유틸리티. 씬에 붙어있지 않으면 동작하지 않는다.
public class JsonDataLoader : MonoBehaviour
{
    public static JsonDataLoader Instance;

    public Dictionary<int, MonsterData> MonsterDict { get; private set; }
    public Dictionary<int, StageData> StageDict { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAllData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadAllData()
    {
        MonsterDict = LoadJson<MonsterData>("Data/MonsterDatas");
        StageDict = LoadJson<StageData>("Data/StageDatas");
    }

    private Dictionary<int, T> LoadJson<T>(string path) where T : IData
    {
        TextAsset jsonText = Resources.Load<TextAsset>(path);

        if (jsonText == null)
        {
            Debug.LogError($"[JsonDataLoader] {path} 파일을 찾을 수 없습니다!");
            return new Dictionary<int, T>();
        }

        List<T> dataList = JsonConvert.DeserializeObject<List<T>>(jsonText.text);
        Dictionary<int, T> dictionary = new Dictionary<int, T>();

        foreach (T data in dataList)
        {
            dictionary.Add(data.Key, data);
        }

        Debug.Log($"<color=cyan>[로딩 완료]</color> {path} -> {dictionary.Count}개");
        return dictionary;
    }
}
