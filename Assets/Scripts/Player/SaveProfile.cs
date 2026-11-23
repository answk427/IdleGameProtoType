using System.IO;
using UnityEngine;

// 세이브 파일을 둘 폴더를 정한다. 에디터에서 검증용 세이브를 사용자 진행도와 분리하기 위한 장치.
//
//   persistentDataPath/player_save.json                  ← 기본 (빌드는 항상 여기)
//   persistentDataPath/profiles/<이름>/player_save.json   ← 에디터에서 프로필을 켰을 때
//
// 프로필은 에디터에서만 켤 수 있다. 값은 SessionState에 있어서 에디터를 끄면 기본으로 돌아온다.
// 플레이 중에는 바꿀 수 없다(돌고 있는 세션이 다른 프로필에 저장하게 되므로).
// 켜고 끄기는 IdleGame/Save Profile 메뉴(SaveDataMenu) 또는 SetActive를 직접 호출.
public static class SaveProfile
{
    // Claude가 Pipeline으로 플레이 모드를 검증할 때 쓰는 프로필
    public const string VerificationProfile = "claude";

    private const string ProfilesFolder = "profiles";

#if UNITY_EDITOR
    private const string SessionKey = "IdleGame.SaveProfile";

    // 없으면 null(기본 프로필)
    public static string ActiveName
    {
        get
        {
            string name = UnityEditor.SessionState.GetString(SessionKey, string.Empty);
            return string.IsNullOrEmpty(name) ? null : name;
        }
    }

    // null 또는 빈 문자열이면 기본 프로필로 돌아간다. 바꾸지 못하면 false.
    public static bool SetActive(string name)
    {
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[SaveProfile] 플레이 중에는 세이브 프로필을 바꿀 수 없습니다.");
            return false;
        }

        if (!string.IsNullOrEmpty(name) && !IsValidName(name))
        {
            Debug.LogError($"[SaveProfile] 쓸 수 없는 프로필 이름: '{name}' (영문/숫자/-/_ 만)");
            return false;
        }

        UnityEditor.SessionState.SetString(SessionKey, name ?? string.Empty);
        Debug.Log(string.IsNullOrEmpty(name)
            ? "[SaveProfile] 기본 프로필로 전환"
            : $"[SaveProfile] '{name}' 프로필로 전환: {DirectoryFor(name)}");
        return true;
    }

    // 경로 밖으로 나가는 이름(.., 구분자)을 막는다
    private static bool IsValidName(string name)
    {
        foreach (char c in name)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
            if (!ok) return false;
        }
        return true;
    }
#else
    // 빌드에는 프로필이 없다
    public static string ActiveName => null;
#endif

    public static bool IsDefault => ActiveName == null;

    public static string SaveDirectory => DirectoryFor(ActiveName);

    private static string DirectoryFor(string name) => name == null
        ? Application.persistentDataPath
        : Path.Combine(Application.persistentDataPath, ProfilesFolder, name);
}
