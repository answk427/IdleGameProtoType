using System;

// Presenter가 아는 확인창의 전부(docs/UI_DESIGN.md 5-7).
public interface IConfirmDialogView
{
    event Action ConfirmTapped;
    event Action CancelTapped;

    void SetContent(string title, string message, string confirmLabel, string cancelLabel);
}
