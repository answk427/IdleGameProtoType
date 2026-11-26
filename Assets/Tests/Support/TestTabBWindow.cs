// WindowManagerTests·TabBarPresenterTests가 여는 탭 창: Exclusive(WindowOptions.Tab, Window 구분, 뒤로가기로 닫힘).
// 테스트용 창을 이 어셈블리에 두는 이유는 TestPopupAWindow 참고.
public sealed class TestTabBArgs : IWindowArgs<TestTabBWindow> { }

public sealed class TestTabBWindow : UIWindow<TestTabBArgs>
{
    public override WindowOptions Options => WindowOptions.Tab;
}
