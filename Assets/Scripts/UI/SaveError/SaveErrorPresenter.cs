using System;
using System.IO;

// 세이브 오류 창의 로직. 게임 시작을 멈췄다는 것과 진행도가 안전하다는 것을 알리고, 게임 종료만 할 수 있다.
//
// 닫는 길이 없다: IWindowContext를 받지 않고, 창 옵션도 바깥 터치·뒤로가기로 닫지 않는다.
// 원인이 그대로면 다시 시도해도 결과가 같고, 복구 수단(백업으로 되돌리기, 확인받고 새로 시작)은 아직 없다(docs/TODO.md "저장").
public sealed class SaveErrorPresenter : IWindowPresenter
{
    public const string Title = "진행도를 불러오지 못했습니다";
    public const string Body = "세이브 파일을 읽을 수 없어 게임을 시작하지 않았습니다. 세이브 파일은 바꾸지 않았습니다.";
    public const string QuitLabel = "게임 종료";

    private readonly ISaveErrorView view;
    private readonly Action quit;
    private bool quitRequested;

    // quit: 빌드는 Application.Quit, 에디터는 플레이 종료(UIRoot가 넣는다).
    public SaveErrorPresenter(ISaveErrorView view, SaveErrorArgs args, Action quit)
    {
        this.view = view ?? throw new ArgumentNullException(nameof(view));
        this.quit = quit ?? throw new ArgumentNullException(nameof(quit));
        if (args == null) throw new ArgumentNullException(nameof(args));

        // 창의 글은 전부 여기서 만든다(현지화를 붙일 한 곳, docs/UI_DESIGN.md 9장).
        view.SetMessage(Title, Body, FormatReason(args));
        view.SetQuitLabel(QuitLabel);
        view.QuitTapped += HandleQuitTapped;
    }

    // 이유 한 줄. 화면에 파일 경로를 내지 않는다(PC 사용자 이름이 들어 있다. 경로는 로그에만 있다).
    // 검사기가 낸 손상 메시지(InvalidDataException)는 경로가 없어서 그대로 보인다. 그 밖의 예외(읽기 오류 등)는
    // 메시지에 경로가 들어 있을 수 있어 형식 이름만 보인다.
    public static string FormatReason(SaveErrorArgs args)
    {
        switch (args.Kind)
        {
            case SaveErrorKind.Unreadable:
                return args.Error is InvalidDataException ? args.Error.Message : $"읽기 오류 ({args.Error.GetType().Name})";
            case SaveErrorKind.StageMissing:
                return $"저장된 스테이지가 게임 데이터에 없음 (스테이지 {args.StageNumber})";
            default:
                return args.Kind.ToString();
        }
    }

    public void Tick() { }

    public void Dispose() => view.QuitTapped -= HandleQuitTapped;

    // 종료가 끝나기 전에 다시 눌러도 한 번만 부른다.
    private void HandleQuitTapped()
    {
        if (quitRequested) return;
        quitRequested = true;
        quit();
    }
}
