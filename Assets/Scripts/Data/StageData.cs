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
    [JsonProperty]public int stageNumber { get; private set; }
    [JsonProperty] public int ID => stageNumber;
    
    [JsonProperty]public int normalMonsterId {get; private set;}
    [JsonProperty]public int bossMonsterId {get; private set;}
    
    [JsonProperty]public int monstersPerEncounter {get; private set;}
    [JsonProperty]public int encountersToComplete {get; private set;}
    [JsonProperty]public float monsterSpacing {get; private set;}
    
    [JsonProperty]public string bgTexturePath{ get; private set;}

    [JsonProperty] public StageClearType clearType { get; private set; }
}
