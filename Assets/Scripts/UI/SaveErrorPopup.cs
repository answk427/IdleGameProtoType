using TMPro;
using UnityEngine;

// 세이브 파일이 손상되어 게임을 시작할 수 없을 때 뜨는 안내 팝업.
public class SaveErrorPopup : UIBase
{
    [SerializeField] private TextMeshProUGUI messageLabel;
    [SerializeField] private string displayMessage = "저장 데이터를 불러올 수 없습니다.\n고객센터에 문의해 주세요.";

    public override void Show()
    {
        if (messageLabel != null) messageLabel.text = displayMessage;
        base.Show();
    }
}
