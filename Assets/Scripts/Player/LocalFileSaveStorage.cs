using System.Collections;
using System.IO;
using UnityEngine;

// ISaveStorage의 로컬 파일 구현
public class LocalFileSaveStorage : ISaveStorage
{
    private static string SavePath => Path.Combine(Application.persistentDataPath, "player_save.json");

    public PlayerSaveData Load()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("[LocalFileSaveStorage] 저장 파일 없음 → 신규 데이터 생성");
            return new PlayerSaveData();
        }

        string json = File.ReadAllText(SavePath);
        var data = JsonUtility.FromJson<PlayerSaveData>(json);
        Debug.Log($"[LocalFileSaveStorage] 불러오기 완료 (레벨 {data.stats.level})");
        return data;
    }

    public IEnumerator Save(PlayerSaveData data)
    {
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[LocalFileSaveStorage] 저장 완료: {SavePath}");
        yield break;
    }

    public void Delete()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
        Debug.Log("[LocalFileSaveStorage] 저장 파일 삭제");
    }
}
