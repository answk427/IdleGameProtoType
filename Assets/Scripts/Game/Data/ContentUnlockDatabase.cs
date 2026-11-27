using System;
using System.Collections.Generic;
using UnityEngine;

// 콘텐츠 해금 조건 표(엑셀 ContentUnlockDatabase 시트 → 이 에셋). 게임은 GameSession.Unlocks로만 읽는다.
[CreateAssetMenu(fileName = "ContentUnlockDatabase", menuName = "Data/Content Unlock Database")]
public class ContentUnlockDatabase : ScriptableObject, IDataTable
{
    public List<ContentUnlockData> entries = new List<ContentUnlockData>();

    private Dictionary<GameContent, ContentUnlockData> lookup;

    // 행이 없으면 null(데이터 오류).
    public ContentUnlockData Get(GameContent content)
    {
        EnsureLookup();
        return lookup.TryGetValue(content, out ContentUnlockData data) ? data : null;
    }

    public void EnsureLookup()
    {
        if (lookup != null) return;

        lookup = new Dictionary<GameContent, ContentUnlockData>();
        foreach (ContentUnlockData e in entries)
        {
            if (e == null) continue;
            lookup[e.Content] = e;
        }
    }

    // 데이터 검사(에디터가 열릴 때, 빌드 직전): 모든 콘텐츠에 행이 있고, 조건이 닿을 수 있는 값인지. 비어 있으면 정상.
    // maxLevel: 레벨 표의 최대 레벨. stageExists: 스테이지 표에 그 번호가 있는가.
    public static List<string> Validate(ContentUnlockDatabase table, int maxLevel, Func<int, bool> stageExists)
    {
        var errors = new List<string>();
        if (table == null)
        {
            errors.Add("[GameData] ContentUnlockDatabase가 없습니다(콘텐츠 해금 조건).");
            return errors;
        }

        foreach (GameContent content in (GameContent[])Enum.GetValues(typeof(GameContent)))
        {
            ContentUnlockData data = table.Get(content);
            if (data == null)
            {
                errors.Add($"[GameData] ContentUnlockDatabase에 {content}의 행이 없습니다. 조건이 없으면 conditionType을 None으로 적으세요(행이 없으면 잠긴다).");
                continue;
            }

            switch (data.ConditionType)
            {
                case UnlockConditionType.None:
                    break;
                case UnlockConditionType.PlayerLevel:
                    if (data.ConditionValue < 1 || data.ConditionValue > maxLevel)
                        errors.Add($"[GameData] {content}: 해금 레벨 {data.ConditionValue}은(는) 1~{maxLevel} 밖이라 열리지 않습니다.");
                    break;
                case UnlockConditionType.StageReached:
                    if (!stageExists(data.ConditionValue))
                        errors.Add($"[GameData] {content}: 해금 스테이지 {data.ConditionValue}가 StageDatabase에 없어 열리지 않습니다.");
                    break;
                default:
                    errors.Add($"[GameData] {content}: 알 수 없는 해금 조건 {data.ConditionType}.");
                    break;
            }
        }

        return errors;
    }

    private void OnValidate()
    {
        lookup = null; // 인스펙터에서 직접 수정하면 캐시 무효화
    }

    Type IDataTable.DataType => typeof(ContentUnlockData);
    int IDataTable.RowCount => entries.Count;
    IData IDataTable.GetRow(int index) => entries[index];
}
