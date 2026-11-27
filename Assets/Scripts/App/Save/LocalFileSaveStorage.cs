using System.Collections;
using System.IO;
using UnityEngine;

// ISaveStorage의 로컬 파일 구현
//
// Load: 파일이 없으면 신규 데이터, 파일이 있는데 온전히 읽을 수 없으면 예외(신규로 대신하지 않는다).
// Save: Load가 받아들이지 않을 값이면 쓰지 않고 기존 파일을 남긴다.
// 폴더는 SaveProfile이 정한다(에디터 검증용 프로필 분리). 호출마다 다시 읽어서 에디터에서 프로필을 바꿔도 따라간다.
public class LocalFileSaveStorage : ISaveStorage
{
    private const string FileName = "player_save.json";

    private static string SavePath => Path.Combine(SaveProfile.SaveDirectory, FileName);

    public PlayerSaveData Load()
    {
        if (!SaveProfile.IsDefault)
        {
            Debug.LogWarning($"[Save] 검증용 프로필 '{SaveProfile.ActiveName}' 사용 중: {SavePath}");
        }

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

        Directory.CreateDirectory(SaveProfile.SaveDirectory); // 프로필 폴더는 처음엔 없다
        File.WriteAllText(SavePath, json);
        Debug.Log($"[LocalFileSaveStorage] 저장 완료: {SavePath}");
        yield break;
    }

    public void Delete()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
        Debug.Log($"[LocalFileSaveStorage] 저장 파일 삭제: {SavePath}");
    }
}
