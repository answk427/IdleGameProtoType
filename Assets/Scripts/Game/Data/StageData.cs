using UnityEngine;
using Newtonsoft.Json;

// 클리어 방식(StageClearType)은 흐름의 입력 StageSpec 옆에 있다(docs/COMBAT_ARCHITECTURE.md 13-4).
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
    // 인카운터를 스폰하는 거리: 아군(대열 앞장) 몸 중심에서 첫 몬스터 피벗까지(월드 유닛). 화면 크기와 무관한 게임 데이터라
    // 기기 화면비가 전투를 바꾸지 않는다(docs/COMBAT_ARCHITECTURE.md 9-3, D18). 둘째부터는 monsterSpacing 간격.
    [SerializeField] private float spawnDistance;
    // 보스전 제한 시간(초, D25). 0이면 없다. 넘기면 전멸과 같다(같은 스테이지 처음부터).
    // 흐름 시간은 단계 단위로 세므로 double이다(float면 0.1 같은 값이 한 단계 늘어난다, EncounterDirector.StepsFor).
    [SerializeField] private double bossTimeLimit;


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
    [JsonProperty("spawnDistance")]
    public float SpawnDistance { get => spawnDistance; private set => spawnDistance = value; }
    [JsonProperty("bossTimeLimit")]
    public double BossTimeLimit { get => bossTimeLimit; private set => bossTimeLimit = value; }

    // 게임플레이 코드에서는 쓰지 않음 — StageDatabaseSyncer가 이 경로로 텍스처를 찾아
    // StageEntry.backgroundTexture에 채워주는 동기화용 입력값.
    [JsonProperty("bgTexturePath")]
    public string BgTexturePath { get => bgTexturePath; private set => bgTexturePath = value; }
}