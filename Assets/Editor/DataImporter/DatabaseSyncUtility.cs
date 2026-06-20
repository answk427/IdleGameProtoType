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
