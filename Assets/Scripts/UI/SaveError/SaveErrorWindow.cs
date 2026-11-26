using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 세이브 오류 창의 View. 받은 글을 그리고 "게임 종료"가 눌렸다고 알리기만 한다(로직은 SaveErrorPresenter).
//
// System 구분의 모달이다. 공용 딤(System −1칸)이 그 밑의 HUD·탭바 터치를 모두 막고, 모달이 떠 있으면
// 키보드 이동도 꺼진다(UIRoot.RefreshNavigation). 바깥 터치와 뒤로가기로 닫히지 않는다(뒤로가기는 이 창이 먹는다).
// 탭 전환도 System 구분을 닫지 않는다(WindowManager.CloseBelow).
public class SaveErrorWindow : UIWindow<SaveErrorArgs>, ISaveErrorView
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private Button quitButton;
    [SerializeField] private TMP_Text quitLabel;

    public override WindowOptions Options => new WindowOptions
    {
        Band = UISortBand.System,
        Modal = true,
        CloseOnOutside = false,
        CloseOnBack = false,
    };

    public event Action QuitTapped;

    protected override void OnInitialize()
    {
        if (quitButton != null) quitButton.onClick.AddListener(() => QuitTapped?.Invoke());
    }

    public void SetMessage(string title, string body, string reason)
    {
        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;
        if (reasonText != null) reasonText.text = reason;
    }

    public void SetQuitLabel(string label)
    {
        if (quitLabel != null) quitLabel.text = label;
    }
}
