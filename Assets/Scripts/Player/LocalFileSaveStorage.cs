using System.Collections;
using System.IO;
using UnityEngine;

public class LocalFileSaveStorage : SaveStorageBase
{
    private static string SavePath => Path.Combine(Application.persistentDataPath, "player_save.json");

    protected override string ReadRawOrNull()
    {
        return File.Exists(SavePath) ? File.ReadAllText(SavePath) : null;
    }

    protected override PlayerSaveData Parse(string raw)
    {
        return JsonUtility.FromJson<PlayerSaveData>(raw);
    }

    public override IEnumerator Save(PlayerSaveData data)
    {
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[LocalFileSaveStorage] 저장 완료: {SavePath}");
        yield break;
    }

    public override void Delete()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
        Debug.Log("[LocalFileSaveStorage] 저장 파일 삭제");
    }
}
