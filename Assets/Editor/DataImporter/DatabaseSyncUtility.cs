using System.IO;
using UnityEditor;
using UnityEngine;

// 여러 IDataSyncer 구현체가 공통으로 쓰는 헬퍼.
// asset이 없으면 새로 생성하거나, 있으면 로드해서 반환한다.
public static class DatabaseSyncUtility
{
    public static TDb CreateDatabaseAsset<TDb>(string dbPath) where TDb : ScriptableObject
    {
        string directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        TDb db = ScriptableObject.CreateInstance<TDb>();
        AssetDatabase.CreateAsset(db, dbPath);
        AssetDatabase.SaveAssets();
        return db;
    }

    // 행동 시간표 연결(엑셀에 없는 값, docs/COMBAT_ARCHITECTURE.md 12절 D50): 기존 연결을 보존하고, 없으면 이름으로 찾는다.
    // 못 찾으면 null(경고). 전투 데이터 검사(CombatDataValidator)가 에디터와 빌드에서 에러로 막는다.
    public const string TimelineFolder = "Assets/Data/Combat/Timelines";

    public static ActionTimelineSet ResolveTimelines(ActionTimelineSet existing, string name, System.Action<string> log)
    {
        if (existing != null) return existing;

        string path = $"{TimelineFolder}/{name}.asset";
        var found = AssetDatabase.LoadAssetAtPath<ActionTimelineSet>(path);
        if (found == null) log?.Invoke($"[SO 동기화 경고] '{name}'의 행동 시간표를 {path} 에서 찾지 못했습니다. 데이터베이스 항목의 timelines에 직접 연결하세요.");
        return found;
    }

    public static TDb LoadOrCreateDatabaseAsset<TDb>(string dbPath, System.Action<string> log) where TDb : ScriptableObject
    {
        TDb db = AssetDatabase.LoadAssetAtPath<TDb>(dbPath);
        if (db == null)
        {
            db = CreateDatabaseAsset<TDb>(dbPath);
            log?.Invoke($"[SO 생성] {dbPath} 가 없어 새로 생성했습니다.");
        }
        return db;
    }
}
