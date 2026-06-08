using UnityEngine;

[CreateAssetMenu(menuName = "Idle Game/Stage Config", fileName = "StageConfig")]
public class StageConfig : ScriptableObject
{
    [SerializeField] private int stageNumber = 1;
    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private int monstersPerEncounter = 3;
    [SerializeField] private int encountersToComplete = 10;
    [SerializeField] private int monsterHp = 10;
    [SerializeField] private int monsterGoldReward = 1;
    [SerializeField] private float monsterSpacing = 1.2f;
    [SerializeField] private int bossHp = 100;
    [SerializeField] private int bossGoldReward = 25;

    [SerializeField] private GameObject bossPrefab;

    [Header("Environment")]
    public Texture2D StageBackgroundTexture;

    public int StageNumber => stageNumber;
    public GameObject MonsterPrefab => monsterPrefab;
    public int MonstersPerEncounter => Mathf.Max(monstersPerEncounter, 1);
    public int EncountersToComplete => Mathf.Max(encountersToComplete, 1);
    public int MonsterHp => Mathf.Max(monsterHp, 1);
    public int MonsterGoldReward => Mathf.Max(monsterGoldReward, 0);
    public float MonsterSpacing => Mathf.Max(monsterSpacing, 0.1f);
    public int BossHp => Mathf.Max(bossHp, 1);
    public int BossGoldReward => Mathf.Max(bossGoldReward, 0);

    public GameObject BossPrefab => bossPrefab;

}
