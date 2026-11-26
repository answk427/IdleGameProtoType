// WindowManagerTests가 여는 창: 모달 팝업(Popup 구분). 같은 구분의 순서를 보려고 TestPopupAWindow와 하나 더 둔다.
// EditMode 테스트 어셈블리는 에디터 전용이라 그 안의 MonoBehaviour는 GameObject에 붙일 수 없다("editor script").
// 그래서 테스트가 있을 때만 컴파일되는(UNITY_INCLUDE_TESTS) 이 어셈블리에 두고, 파일 이름 = 클래스 이름으로 창마다 파일을 둔다.
public sealed class TestPopupBArgs : IWindowArgs<TestPopupBWindow> { }

public sealed class TestPopupBWindow : UIWindow<TestPopupBArgs>
{
    public override WindowOptions Options => WindowOptions.ModalPopup;
}
