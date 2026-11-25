using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 개발 도구의 세이브 백업(docs/DEV_TOOLS.md T7).
//
//   <세이브 폴더>/dev_backups/<시각>_<이유>.json
//
// 세이브 폴더는 호출하는 쪽이 넘긴다(게임에서는 SaveProfile.SaveDirectory라 에디터의 검증용 프로필 분리가 유지된다).
// 형식은 세이브 파일과 같다(PlayerSaveData JSON). 쓸 때와 읽을 때 모두 SaveDataValidator를 통과해야 한다.
// 지우는 기능은 두지 않는다. 백업을 지우는 것도 진행도를 지우는 것이다.
public sealed class DevBackupStore
{
    public const string FolderName = "dev_backups";

    public DevBackupStore(string saveDirectory)
    {
        Directory = Path.Combine(saveDirectory, FolderName);
    }

    public string Directory { get; }

    // 검사를 통과하지 못하는 값이면 쓰지 않는다. 같은 이름이 있으면 덮어쓰지 않고 번호를 붙인다.
    public bool TryWrite(PlayerSaveData data, string reason, out string path, out string error)
    {
        path = null;
        if (data == null) { error = "백업할 데이터가 없습니다."; return false; }

        string json = JsonUtility.ToJson(data, true);
        if (!SaveDataValidator.TryValidate(json, out error))
        {
            error = $"검사를 통과하지 못해 백업하지 않았습니다: {error}";
            return false;
        }

        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            path = UniquePath($"{DateTime.Now:yyyyMMdd-HHmmss-fff}_{Sanitize(reason)}");

            // 쓰는 도중 끊겨도 잘린 백업이 목록에 남지 않게, 임시 파일에 다 쓴 뒤 이름을 바꾼다.
            string temp = path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, path);
            return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            path = null;
            error = $"백업 파일을 쓰지 못했습니다: {e.Message}";
            return false;
        }
    }

    // 최근 것부터. 이름이 시각으로 시작해서 이름순이 곧 시간순이다.
    public IReadOnlyList<string> List()
    {
        if (!System.IO.Directory.Exists(Directory)) return Array.Empty<string>();

        string[] files = System.IO.Directory.GetFiles(Directory, "*.json");
        Array.Sort(files, StringComparer.Ordinal);
        Array.Reverse(files);
        return files;
    }

    // 검사를 통과한 백업만 읽는다. 실패하면 data는 null이다.
    public bool TryRead(string path, out PlayerSaveData data, out string error)
    {
        data = null;
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            error = $"백업 파일을 읽지 못했습니다: {e.Message}";
            return false;
        }

        if (!SaveDataValidator.TryValidate(json, out error))
        {
            error = $"손상된 백업이라 쓰지 않습니다: {error}";
            return false;
        }

        data = JsonUtility.FromJson<PlayerSaveData>(json);
        return true;
    }

    private string UniquePath(string stem)
    {
        string path = Path.Combine(Directory, stem + ".json");
        for (int n = 2; File.Exists(path); n++)
            path = Path.Combine(Directory, $"{stem}-{n}.json");
        return path;
    }

    // 이유는 파일 이름에 들어가므로 영문·숫자·-·_만 남긴다.
    private static string Sanitize(string reason)
    {
        if (string.IsNullOrEmpty(reason)) return "backup";

        var chars = reason.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
            if (!ok) chars[i] = '-';
        }
        return new string(chars);
    }
}
