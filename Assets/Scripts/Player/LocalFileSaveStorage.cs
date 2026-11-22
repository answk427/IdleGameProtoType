using System.Collections;
using System.IO;
using UnityEngine;

// ISaveStorage의 로컬 파일 구현
//
// Load: 파일이 없으면 신규 데이터, 파일이 있는데 온전히 읽을 수 없으면 예외(신규로 대신하지 않는다).
// Save: Load가 받아들이지 않을 값이면 쓰지 않고 기존 파일을 남긴다.
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
        if (!SaveDataValidator.TryValidate(json, out string error))
        {
            throw new InvalidDataException($"세이브 파일이 손상되었습니다: {error}");
        }

        var data = JsonUtility.FromJson<PlayerSaveData>(json);
        Debug.Log($"[LocalFileSaveStorage] 불러오기 완료 (레벨 {data.stats.level})");
        return data;
    }

    public IEnumerator Save(PlayerSaveData data)
    {
        string json = JsonUtility.ToJson(data, prettyPrint: true);

        // JsonUtility는 NaN/Infinity도 그대로 쓴다. 그런 파일은 다음 실행에서 막히므로 마지막 정상본을 남긴다.
        if (!SaveDataValidator.TryValidate(json, out string error))
        {
            Debug.LogError($"[LocalFileSaveStorage] 저장하지 않음, 기존 파일 유지: {error}");
            yield break;
        }

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
