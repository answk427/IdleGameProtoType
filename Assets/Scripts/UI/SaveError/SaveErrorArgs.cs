using System;

// 시작을 멈춘 이유. 둘 다 "세이브를 지키려고 시작하지 않는" 경우라 같은 창을 쓰고 이유 줄만 다르다.
public enum SaveErrorKind
{
    Unreadable,    // 세이브를 읽지 못함(손상, 읽기 오류): ISaveStorage.Load()의 예외
    StageMissing,  // 저장된 스테이지가 게임 데이터(StageDatabase)에 없음
}

// 세이브 오류 창의 인자. 표시 문자열은 들고 있지 않다(문구는 SaveErrorPresenter가 만든다).
public sealed class SaveErrorArgs : IWindowArgs<SaveErrorWindow>
{
    public SaveErrorKind Kind { get; }

    // Unreadable일 때 Load()가 던진 예외. 메시지를 그대로 화면에 낼지는 Presenter가 정한다(경로가 들어 있을 수 있다).
    public Exception Error { get; }

    // StageMissing일 때 저장된 스테이지 번호.
    public int StageNumber { get; }

    private SaveErrorArgs(SaveErrorKind kind, Exception error, int stageNumber)
    {
        Kind = kind;
        Error = error;
        StageNumber = stageNumber;
    }

    public static SaveErrorArgs Unreadable(Exception error) =>
        new SaveErrorArgs(SaveErrorKind.Unreadable, error ?? throw new ArgumentNullException(nameof(error)), 0);

    public static SaveErrorArgs StageMissing(int stageNumber) =>
        new SaveErrorArgs(SaveErrorKind.StageMissing, null, stageNumber);
}
