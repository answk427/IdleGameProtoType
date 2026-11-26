using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 확인창의 View. 받은 글을 그리고 버튼이 눌렸다고 알리기만 한다(로직은 ConfirmDialogPresenter, 여는 길은 DialogService).
// 모달 팝업: 딤이 밑의 터치를 막고, 바깥 터치와 뒤로가기는 취소(결과 없이 닫힘 → DialogService가 false로 바꾼다).
public class ConfirmDialogWindow : UIWindow<ConfirmArgs, bool>, IConfirmDialogView
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private UIButton confirmButton;
    [SerializeField] private TMP_Text confirmLabel;
    [SerializeField] private UIButton cancelButton;
    [SerializeField] private TMP_Text cancelLabel;

    public override WindowOptions Options => WindowOptions.ModalPopup;

    public event Action ConfirmTapped;
    public event Action CancelTapped;

    protected override void OnInitialize()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(() => ConfirmTapped?.Invoke());
        if (cancelButton != null) cancelButton.onClick.AddListener(() => CancelTapped?.Invoke());
    }

    public void SetContent(string title, string message, string confirm, string cancel)
    {
        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;
        if (confirmLabel != null) confirmLabel.text = confirm;
        if (cancelLabel != null) cancelLabel.text = cancel;
    }
}
