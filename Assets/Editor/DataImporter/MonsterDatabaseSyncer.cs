using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// MonsterData → MonsterDatabase(SO) 동기화 담당.
// monsterName 기준으로 프리팹을 자동 매칭해서 MonsterEntry.prefab을 채운다.
// groundOffset은 자동 매칭 대상이 아니므로(스프라이트 피벗에 따라 디자이너가 수동 조정),
// 기존에 설정되어 있던 값은 재동기화 시에도 보존한다.
// ExcelToJsonConverter는 이 클래스를 IDataSyncer 구현체로 리플렉션 탐색해 자동으로 실행한다.
public class MonsterDatabaseSyncer : IDataSyncer
{
    private const string DbPath = "Assets/Data/Monster/MonsterDatabase.asset";

    public Type DataType => typeof(MonsterData);

    public void Sync(IList dataList, Action<string> log)
    {
        var db = DatabaseSyncUtility.LoadOrCreateDatabaseAsset<MonsterDatabase>(DbPath, log);

        // 기존 entry를 id 기준으로 백업 (groundOffset 보존용)
        var existingById = new Dictionary<int, MonsterEntry>();
        foreach (var e in db.entries)
        {
            if (e?.data != null) existingById[e.data.Id] = e;
        }

        var newEntries = new List<MonsterEntry>();
        int matchedPrefab = 0;
        int preserved = 0;

        foreach (MonsterData data in dataList)
        {
            string prefabPath = $"Assets/Resources/Prefabs/Monsters/{data.MonsterName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab != null) matchedPrefab++;
            else log?.Invoke($"[SO 동기화 경고] '{data.MonsterName}' 프리팹을 {prefabPath} 에서 찾지 못했습니다.");

            float groundOffset = 0f;
            if (existingById.TryGetValue(data.Id, out var existing))
            {
                groundOffset = existing.groundOffset;
                preserved++;
            }

            newEntries.Add(new MonsterEntry { data = data, prefab = prefab, groundOffset = groundOffset });
        }

        db.entries = newEntries;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        log?.Invoke($"\u2705 MonsterDatabase 동기화 완료: {newEntries.Count}개 (프리팹 매칭 {matchedPrefab}개, groundOffset 유지 {preserved}개)");
    }
}
