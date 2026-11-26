using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

// ContentUnlockData → ContentUnlockDatabase(SO) 동기화(docs/UI_DESIGN.md 10-6). 엑셀에 없는 값이 없어서 행을 통째로 바꾼다.
// ExcelToJsonConverter는 이 클래스를 IDataSyncer 구현체로 리플렉션 탐색해 자동으로 실행한다.
public class ContentUnlockDatabaseSyncer : IDataSyncer
{
    private const string DbPath = "Assets/Data/Progress/ContentUnlockDatabase.asset";

    public Type DataType => typeof(ContentUnlockData);

    public void Sync(IList dataList, Action<string> log)
    {
        var db = DatabaseSyncUtility.LoadOrCreateDatabaseAsset<ContentUnlockDatabase>(DbPath, log);

        var entries = new List<ContentUnlockData>();
        foreach (ContentUnlockData data in dataList) entries.Add(data);
        entries.Sort((a, b) => a.Key.CompareTo(b.Key));

        db.entries = entries;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        log?.Invoke($"✅ ContentUnlockDatabase 동기화 완료: {entries.Count}개");
    }
}
