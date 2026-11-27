using System;

// 엔진 없는 코드(Core, Simulation)의 로그 접점(docs/COMBAT_ARCHITECTURE.md 13-5, D37).
// 밖으로 내기만 한다: 부르는 쪽은 아무것도 돌려받지 않는다(UIButtonEvents와 같은 예외, 정적으로 서비스를 찾는 길이 아니다).
// 앱이 시작할 때 받는 쪽(UnityLogSink → Debug.Log*)을 넣는다. 넣기 전이나 Unity 밖에서는 표준 오류 출력으로 낸다(버리지 않는다).
public static class GameLog
{
    private static IGameLogSink sink = StandardErrorSink.Instance;

    // 받는 쪽을 바꾼다. null이면 표준 오류 출력으로 돌아간다.
    public static void SetSink(IGameLogSink newSink) => sink = newSink ?? StandardErrorSink.Instance;

    public static void Info(string message) => sink.Write(GameLogLevel.Info, message);
    public static void Warning(string message) => sink.Write(GameLogLevel.Warning, message);
    public static void Error(string message) => sink.Write(GameLogLevel.Error, message);
    public static void Exception(Exception exception) => sink.WriteException(exception);

    private sealed class StandardErrorSink : IGameLogSink
    {
        public static readonly StandardErrorSink Instance = new StandardErrorSink();

        public void Write(GameLogLevel level, string message) => Console.Error.WriteLine($"[{level}] {message}");
        public void WriteException(Exception exception) => Console.Error.WriteLine(exception);
    }
}

public enum GameLogLevel
{
    Info,
    Warning,
    Error,
}

public interface IGameLogSink
{
    void Write(GameLogLevel level, string message);
    void WriteException(Exception exception);
}
