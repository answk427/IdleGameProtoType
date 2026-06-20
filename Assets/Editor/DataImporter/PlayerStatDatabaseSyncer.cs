using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// PlayerStatData → PlayerStatDatabase(SO) 동기화 담당.
// 레벨 순으로 정렬해서 entries에 채운다 (프리팹/텍스처 같은 외부 참조 매칭은 필요 없음).
// ExcelToJsonConverter는 이 클래스를 IDataSyncer 구현체로 리플렉션 탐색해 자동으로 실행한다.
public class PlayerStatDatabaseSyncer : IDataSyncer
{
    private const string DbPath = "Assets/Data/Player/PlayerStatDatabase.asset";

    public Type DataType => typeof(PlayerStatData);

    public void Sync(IList dataList, Action<string> log)
    {
        var db = DatabaseSyncUtility.LoadOrCreateDatabaseAsset<PlayerStatDatabase>(DbPath, log);

        var newEntries = new List<PlayerStatData>();
        foreach (PlayerStatData data in dataList)
        {
            newEntries.Add(data);
        }
        newEntries.Sort((a, b) => a.level.CompareTo(b.level));

        db.entries = newEntries;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        log?.Invoke($"\u2705 PlayerStatDatabase 동기화 완료: {newEntries.Count}개 레벨");
    }
}
