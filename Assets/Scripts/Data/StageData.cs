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
    public int Key => StageNumber;

    [SerializeField] private int stageNumber;
    [SerializeField] private int normalMonsterId;
    [SerializeField] private int bossMonsterId;
    [SerializeField] private int monstersPerEncounter;
    [SerializeField] private int encountersToComplete;
    [SerializeField] private float monsterSpacing;
    [SerializeField] private string bgTexturePath;
    [SerializeField] private StageClearType clearType;
    
    
    [JsonProperty("clearType")]
    public StageClearType ClearType { get => clearType; private set => clearType = value; }
    [JsonProperty("stageNumber")]
    public int StageNumber { get => stageNumber; private set => stageNumber = value; }
    [JsonProperty("normalMonsterId")]
    public int NormalMonsterId { get => normalMonsterId; private set => normalMonsterId = value; }
    [JsonProperty("bossMonsterId")]
    public int BossMonsterId { get => bossMonsterId; private set => bossMonsterId = value; }
    [JsonProperty("monstersPerEncounter")]
    public int MonstersPerEncounter { get => monstersPerEncounter; private set => monstersPerEncounter = value; }
    [JsonProperty("encountersToComplete")]
    public int EncountersToComplete { get => encountersToComplete; private set => encountersToComplete = value; }
    [JsonProperty("monsterSpacing")]
    public float MonsterSpacing { get => monsterSpacing; private set => monsterSpacing = value; }
    
    // 게임플레이 코드에서는 쓰지 않음 — StageDatabaseSyncer가 이 경로로 텍스처를 찾아
    // StageEntry.backgroundTexture에 채워주는 동기화용 입력값.
    [JsonProperty("bgTexturePath")]
    public string BgTexturePath { get => bgTexturePath; private set => bgTexturePath = value; }
}