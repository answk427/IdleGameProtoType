using System;
using UnityEngine;

// 엔진 없는 코드(Core, Simulation)의 로그(GameLog)를 Unity 콘솔로 넘긴다(docs/COMBAT_ARCHITECTURE.md 13-5).
// 메시지는 그대로라 Debug.Log*로 직접 남긴 것과 같다(테스트의 LogAssert도 그대로 맞는다).
// 플레이·빌드는 씬을 읽기 전에, 에디터는 스크립트를 읽을 때(EditMode 테스트) 넣는다.
public sealed class UnityLogSink : IGameLogSink
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
#endif
    private static void Install() => GameLog.SetSink(new UnityLogSink());

    public void Write(GameLogLevel level, string message)
    {
        switch (level)
        {
            case GameLogLevel.Error: Debug.LogError(message); break;
            case GameLogLevel.Warning: Debug.LogWarning(message); break;
            default: Debug.Log(message); break;
        }
    }

    public void WriteException(Exception exception) => Debug.LogException(exception);
}
