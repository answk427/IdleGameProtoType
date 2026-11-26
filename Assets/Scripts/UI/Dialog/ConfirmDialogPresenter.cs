using System;

// 확인창의 로직. 확인 = true, 취소 = false로 닫는다. 뒤로가기·바깥 터치는 창 옵션(ModalPopup)이 닫는다(결과 없음 = 취소).
public sealed class ConfirmDialogPresenter : IWindowPresenter
{
    public const string DefaultConfirmLabel = "확인";
    public const string DefaultCancelLabel = "취소";

    private readonly IConfirmDialogView view;
    private readonly IWindowContext<bool> window;

    public ConfirmDialogPresenter(IConfirmDialogView view, ConfirmArgs args, IWindowContext<bool> window)
    {
        this.view = view ?? throw new ArgumentNullException(nameof(view));
        this.window = window ?? throw new ArgumentNullException(nameof(window));
        if (args == null) throw new ArgumentNullException(nameof(args));

        view.SetContent(
            args.Title ?? string.Empty,
            args.Message ?? string.Empty,
            string.IsNullOrEmpty(args.ConfirmLabel) ? DefaultConfirmLabel : args.ConfirmLabel,
            string.IsNullOrEmpty(args.CancelLabel) ? DefaultCancelLabel : args.CancelLabel);

        view.ConfirmTapped += HandleConfirmTapped;
        view.CancelTapped += HandleCancelTapped;
    }

    public void Tick() { }

    public void Dispose()
    {
        view.ConfirmTapped -= HandleConfirmTapped;
        view.CancelTapped -= HandleCancelTapped;
    }

    // 닫힌 뒤에 다시 불려도 WindowManager가 무시한다(같은 열기의 결과는 한 번만).
    private void HandleConfirmTapped() => window.Close(true);

    private void HandleCancelTapped() => window.Close(false);
}
