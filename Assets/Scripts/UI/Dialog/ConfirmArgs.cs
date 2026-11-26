// 확인창의 인자. 글은 부르는 쪽(Presenter 등)이 만든다(현지화를 붙일 자리, docs/UI_DESIGN.md 9장).
// 버튼 글을 비우면 ConfirmDialogPresenter의 기본 글("확인", "취소")을 쓴다.
public sealed class ConfirmArgs : IWindowArgs<ConfirmDialogWindow, bool>
{
    public string Title { get; }
    public string Message { get; }
    public string ConfirmLabel { get; }
    public string CancelLabel { get; }

    public ConfirmArgs(string title, string message, string confirmLabel = null, string cancelLabel = null)
    {
        Title = title;
        Message = message;
        ConfirmLabel = confirmLabel;
        CancelLabel = cancelLabel;
    }
}
