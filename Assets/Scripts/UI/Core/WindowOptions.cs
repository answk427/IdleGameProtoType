// 창이 스택에 들어가는 방식 (docs/UI_DESIGN.md 6-4).
public enum WindowStackMode
{
    // 지금 스택 위에 쌓인다(팝업).
    Overlay,

    // 열릴 때 System 구분 밑의 창을 닫고 바닥이 된다(탭). 닫는 것은 자기보다 먼저 열기를 요청한 창뿐이다.
    Exclusive,
}

// 창의 구조(서로 독립인 축들). 동작이 프리팹 YAML에 숨지 않도록 코드로만 정한다 (docs/UI_DESIGN.md 6-1).
// 프리셋을 받아서 필요한 축만 고쳐 쓴다.
//
// 전체 화면, 캐시 정책 축은 그것을 쓰는 첫 창이 생길 때 추가한다. 그때까지 인스턴스는 재사용된다.
public struct WindowOptions
{
    public UISortBand Band;

    public WindowStackMode Stack;

    // 아래 화면을 어둡게 하고 입력을 막는다.
    public bool Modal;

    // 모달 바깥(딤)을 누르면 닫는다.
    public bool CloseOnOutside;

    // 뒤로가기로 닫는다. 꺼도 뒤로가기는 이 창이 먹는다(밑의 창으로 넘어가지 않는다).
    public bool CloseOnBack;

    public static WindowOptions ModalPopup => new WindowOptions
    {
        Band = UISortBand.Popup,
        Stack = WindowStackMode.Overlay,
        Modal = true,
        CloseOnOutside = true,
        CloseOnBack = true,
    };

    // 하단 탭의 내용. 모달이 아니라 밑의 HUD와 탭바를 그대로 누를 수 있다.
    public static WindowOptions Tab => new WindowOptions
    {
        Band = UISortBand.Window,
        Stack = WindowStackMode.Exclusive,
        Modal = false,
        CloseOnOutside = false,
        CloseOnBack = true,
    };
}
