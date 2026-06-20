using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance;

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
            Debug.LogError($"[DataManager] {path} 파일을 찾을 수 없습니다!");
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
