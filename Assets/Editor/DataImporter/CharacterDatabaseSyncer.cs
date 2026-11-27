using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// CharacterData → CharacterDatabase(SO) 동기화 담당(docs/COMBAT_ARCHITECTURE.md 8-7, D12).
// characterName 기준으로 플레이어 프리팹을 자동 연결하고, 찾지 못하면 기존 연결을 유지한다(경고).
// ExcelToJsonConverter는 이 클래스를 IDataSyncer 구현체로 리플렉션 탐색해 자동으로 실행한다.
public class CharacterDatabaseSyncer : IDataSyncer
{
    private const string DbPath = "Assets/Data/Player/CharacterDatabase.asset";
    private const string PrefabFolder = "Assets/Resources/Prefabs/Player";

    public Type DataType => typeof(CharacterData);

    public void Sync(IList dataList, Action<string> log)
    {
        var db = DatabaseSyncUtility.LoadOrCreateDatabaseAsset<CharacterDatabase>(DbPath, log);

        var existingById = new Dictionary<int, CharacterEntry>();
        foreach (var e in db.entries)
        {
            if (e?.data != null) existingById[e.data.Id] = e;
        }

        var newEntries = new List<CharacterEntry>();
        int matchedPrefab = 0;

        foreach (CharacterData data in dataList)
        {
            string prefabPath = $"{PrefabFolder}/{data.CharacterName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            existingById.TryGetValue(data.Id, out var existing);

            if (prefab != null) matchedPrefab++;
            else if (existing?.prefab != null)
            {
                prefab = existing.prefab;
                log?.Invoke($"[SO 동기화 경고] '{data.CharacterName}' 프리팹을 {prefabPath} 에서 찾지 못해 기존 연결({prefab.name})을 유지합니다. characterName과 프리팹 이름을 맞추세요.");
            }
            else log?.Invoke($"[SO 동기화 경고] '{data.CharacterName}' 프리팹을 {prefabPath} 에서 찾지 못했습니다.");

            ActionTimelineSet timelines = DatabaseSyncUtility.ResolveTimelines(existing?.timelines, data.CharacterName, log);
            newEntries.Add(new CharacterEntry { data = data, prefab = prefab, timelines = timelines });
        }
        newEntries.Sort((a, b) => a.data.Id.CompareTo(b.data.Id));

        db.entries = newEntries;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        log?.Invoke($"✅ CharacterDatabase 동기화 완료: {newEntries.Count}개 (프리팹 매칭 {matchedPrefab}개)");
    }
}
