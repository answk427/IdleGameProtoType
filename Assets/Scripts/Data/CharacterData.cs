using Newtonsoft.Json;

// 플레이어 캐릭터(직업) 한 종의 속성(docs/COMBAT_ARCHITECTURE.md 8-7, D12). 원본은 엑셀 CharacterDatabase 시트.
// 레벨에 따라 변하는 스탯(HP, 공격력, 공격 간격)은 PlayerStatData, 캐릭터마다 다른 속성은 여기다.
// 엑셀 열 이름 = 필드 이름. 필드 이름을 바꾸면 엑셀 열과 .asset을 같이 맞출 것(CLAUDE.md "직렬화 함정").
[System.Serializable]
public class CharacterData : IData
{
    public int Key => id;

    [UnityEngine.SerializeField] private int id;
    [UnityEngine.SerializeField] private string characterName; // 프리팹 이름(Assets/Resources/Prefabs/Player/<이름>.prefab)
    [UnityEngine.SerializeField] private float attackRange;    // 표면 거리 기준
    [UnityEngine.SerializeField] private float moveSpeed;      // 월드 유닛/초. 3단계 전까지는 스크롤 속도

    [JsonProperty("id")]
    public int Id { get => id; private set => id = value; }
    [JsonProperty("characterName")]
    public string CharacterName { get => characterName; private set => characterName = value; }
    [JsonProperty("attackRange")]
    public float AttackRange { get => attackRange; private set => attackRange = value; }
    [JsonProperty("moveSpeed")]
    public float MoveSpeed { get => moveSpeed; private set => moveSpeed = value; }
}
