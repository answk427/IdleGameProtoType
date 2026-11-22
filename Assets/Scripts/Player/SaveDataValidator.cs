using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// 세이브 JSON이 PlayerSaveData로 온전히 변환될 수 있는지 변환 "전에" 검사한다.
//
// JsonUtility는 없는 필드를 기본값으로, 타입이 다른 값을 0으로 채우기 때문에 변환한 뒤에는 손상을 알 수 없다.
// 그대로 시작하면 레벨 1 데이터가 종료 시 원본을 덮어쓴다.
//
// 키는 nameof로 DTO 필드와 묶는다. 필드 이름을 바꾸면 옛 세이브가 기본값으로 읽히는 대신 여기서 막힌다.
// 새 필드는 옛 세이브에 없으므로, 필수로 추가하려면 버전 변환이 먼저 필요하다(docs/TODO.md "저장").
public static class SaveDataValidator
{
    // 같은 키가 두 번 있으면 JObject는 마지막 값을, JsonUtility는 첫 값을 읽는다.
    // 검사한 값과 실제로 쓰는 값이 달라지므로 손상으로 본다.
    private static readonly JsonLoadSettings LoadSettings = new JsonLoadSettings
    {
        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
    };

    public static bool TryValidate(string json, out string error)
    {
        JObject root;
        try
        {
            root = JObject.Parse(json, LoadSettings);
        }
        catch (JsonException e)
        {
            error = $"JSON을 해석할 수 없음 ({e.Message})";
            return false;
        }

        error = FindError(root);
        return error == null;
    }

    private static string FindError(JObject root)
    {
        if (!(root[nameof(PlayerSaveData.stats)] is JObject stats))
            return $"{nameof(PlayerSaveData.stats)}: 없거나 객체가 아님";

        return CheckAmount(root, nameof(PlayerSaveData.gold))
            ?? CheckInt(root, nameof(PlayerSaveData.currentStageNumber), min: 1)
            ?? CheckInt(stats, nameof(PlayerStatsSaveData.level), min: 1)
            ?? CheckInt(stats, nameof(PlayerStatsSaveData.currentExp), min: 0)
            ?? CheckInt(stats, nameof(PlayerStatsSaveData.hpUpgradeLevel), min: 0)
            ?? CheckInt(stats, nameof(PlayerStatsSaveData.attackUpgradeLevel), min: 0)
            ?? CheckInt(stats, nameof(PlayerStatsSaveData.attackSpeedUpgradeLevel), min: 0)
            ?? CheckIntArray(stats, nameof(PlayerStatsSaveData.learnedSkillIds), min: int.MinValue)
            ?? CheckIntArray(stats, nameof(PlayerStatsSaveData.equippedSkillSlotIds),
                min: PlayerStatsSaveData.EmptySlot, length: PlayerStatsSaveData.SkillSlotCount);
    }

    // 상한은 검사하지 않는다. 밸런스 데이터가 줄었을 때 정상 세이브까지 손상으로 판정하게 되기 때문이다.
    private static string CheckInt(JObject parent, string key, int min)
    {
        return IsInt(parent[key], min) ? null : $"{key}: 없거나 {min} 이상의 정수가 아님";
    }

    // 골드는 double이지만 예전 세이브에는 정수로 적혀 있다.
    private static string CheckAmount(JObject parent, string key)
    {
        JToken token = parent[key];
        if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
            return $"{key}: 없거나 숫자가 아님";

        double value = (double)token;
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            return $"{key}: 0 이상의 유한한 수가 아님";

        return null;
    }

    private static string CheckIntArray(JObject parent, string key, int min, int? length = null)
    {
        if (!(parent[key] is JArray array)) return $"{key}: 없거나 배열이 아님";
        if (length.HasValue && array.Count != length.Value) return $"{key}: 길이 {length.Value} 필요, 현재 {array.Count}";

        foreach (JToken item in array)
        {
            if (!IsInt(item, min)) return $"{key}: 올바르지 않은 값이 있음";
        }
        return null;
    }

    // int 범위까지 검사한다. JsonUtility는 범위를 넘는 수를 에러 없이 잘라서 넣는다.
    private static bool IsInt(JToken token, int min)
    {
        return token is JValue value && value.Value is long number && number >= min && number <= int.MaxValue;
    }
}
