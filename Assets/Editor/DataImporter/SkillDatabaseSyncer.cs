using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// SkillData → SkillDatabase(SO) 동기화 담당.
// id순으로 정렬해서 entries에 채운다 (프리팹/텍스처 같은 외부 참조 매칭은 필요 없음).
// ExcelToJsonConverter는 이 클래스를 IDataSyncer 구현체로 리플렉션 탐색해 자동으로 실행한다.
public class SkillDatabaseSyncer : IDataSyncer
{
    private const string DbPath = "Assets/Data/Skill/SkillDatabase.asset";

    public Type DataType => typeof(SkillData);

    public void Sync(IList dataList, Action<string> log)
    {
        var db = DatabaseSyncUtility.LoadOrCreateDatabaseAsset<SkillDatabase>(DbPath, log);

        var newEntries = new List<SkillData>();
        foreach (SkillData data in dataList)
        {
            newEntries.Add(data);
        }
        newEntries.Sort((a, b) => a.id.CompareTo(b.id));

        db.entries = newEntries;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        log?.Invoke($"\u2705 SkillDatabase 동기화 완료: {newEntries.Count}개");
    }
}
