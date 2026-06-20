using UnityEngine;
using Newtonsoft.Json;

public enum StageClearType
{
    KillBoss,       // 일반적인 보스 처치
    SurviveTime,    // 특정 시간 동안 생존
    KillCount       // 특정 마릿수 처치
}

[System.Serializable]
public class StageData : IData
{
    public int Key => stageNumber;

    [SerializeField] private int _stageNumber;
    [SerializeField] private int _normalMonsterId;
    [SerializeField] private int _bossMonsterId;
    [SerializeField] private int _monstersPerEncounter;
    [SerializeField] private int _encountersToComplete;
    [SerializeField] private float _monsterSpacing;
    [SerializeField] private string _bgTexturePath;
    [SerializeField] private StageClearType _clearType;
    
    
    [JsonProperty("clearType")]
    public StageClearType clearType { get => _clearType; private set => _clearType = value; }
    [JsonProperty("stageNumber")]
    public int stageNumber { get => _stageNumber; private set => _stageNumber = value; }
    [JsonProperty("normalMonsterId")]
    public int normalMonsterId { get => _normalMonsterId; private set => _normalMonsterId = value; }
    [JsonProperty("bossMonsterId")]
    public int bossMonsterId { get => _bossMonsterId; private set => _bossMonsterId = value; }
    [JsonProperty("monstersPerEncounter")]
    public int monstersPerEncounter { get => _monstersPerEncounter; private set => _monstersPerEncounter = value; }
    [JsonProperty("encountersToComplete")]
    public int encountersToComplete { get => _encountersToComplete; private set => _encountersToComplete = value; }
    [JsonProperty("monsterSpacing")]
    public float monsterSpacing { get => _monsterSpacing; private set => _monsterSpacing = value; }
    [JsonProperty("bgTexturePath")]
    public string bgTexturePath { get => _bgTexturePath; private set => _bgTexturePath = value; }
}