using Newtonsoft.Json;

// 해금되는 게임 콘텐츠(docs/UI_DESIGN.md 10-6). 게임에 실제로 있는 것만 둔다(장비·동료·모험·상점은 아직 게임에 없다).
// UI의 탭(UITabType)과는 다른 것이다: 탭 → 콘텐츠의 짝은 UIRoot가 맺는다(UI → 게임 한 방향).
// 값은 .asset에 정수로 저장되므로 번호를 바꾸지 않는다(CLAUDE.md "직렬화 함정"). 엑셀에는 이름으로 쓴다.
public enum GameContent
{
    Character = 1,
    Skill = 2,
}

public enum UnlockConditionType
{
    None = 0,         // 처음부터 열림
    PlayerLevel = 1,  // 플레이어 레벨 ≥ 값
    StageReached = 2, // 스테이지 값에 도달(저장된 스테이지 번호 ≥ 값). "클리어"는 진행도로 알 수 없다(마지막 스테이지는 깨도 그대로)
}

// 콘텐츠 하나의 해금 조건. 원본은 엑셀 ContentUnlockDatabase 시트. 모든 콘텐츠에 행이 있어야 한다(없으면 데이터 오류, 잠김).
// 엑셀 열 이름 = 필드 이름. 필드 이름을 바꾸면 엑셀 열과 .asset을 같이 맞출 것(CLAUDE.md "직렬화 함정").
[System.Serializable]
public class ContentUnlockData : IData
{
    public int Key => (int)content;

    [UnityEngine.SerializeField] private GameContent content;
    [UnityEngine.SerializeField] private UnlockConditionType conditionType;
    [UnityEngine.SerializeField] private int conditionValue;

    [JsonProperty("content")]
    public GameContent Content { get => content; private set => content = value; }
    [JsonProperty("conditionType")]
    public UnlockConditionType ConditionType { get => conditionType; private set => conditionType = value; }
    [JsonProperty("conditionValue")]
    public int ConditionValue { get => conditionValue; private set => conditionValue = value; }

    [JsonIgnore] // 계산한 값이라 JSON에 쓰지 않는다
    public UnlockCondition Condition => new UnlockCondition(conditionType, conditionValue);
}

// 해금 조건 값(표시와 판정에 같이 쓴다).
public readonly struct UnlockCondition : System.IEquatable<UnlockCondition>
{
    public readonly UnlockConditionType Type;
    public readonly int Value;

    public UnlockCondition(UnlockConditionType type, int value)
    {
        Type = type;
        Value = value;
    }

    public static UnlockCondition None => new UnlockCondition(UnlockConditionType.None, 0);

    // 진행도(레벨, 저장된 스테이지 번호)가 이 조건을 채우는가.
    public bool IsMetBy(int level, int stageNumber)
    {
        switch (Type)
        {
            case UnlockConditionType.None: return true;
            case UnlockConditionType.PlayerLevel: return level >= Value;
            case UnlockConditionType.StageReached: return stageNumber >= Value;
            default: return false;
        }
    }

    public bool Equals(UnlockCondition other) => Type == other.Type && Value == other.Value;
    public override bool Equals(object obj) => obj is UnlockCondition other && Equals(other);
    public override int GetHashCode() => ((int)Type * 397) ^ Value;
    public override string ToString() => $"{Type} {Value}";
}
