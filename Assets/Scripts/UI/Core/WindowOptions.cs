// 창의 구조(서로 독립인 축들). 동작이 프리팹 YAML에 숨지 않도록 코드로만 정한다 (docs/UI_DESIGN.md 6-1).
// 프리셋을 받아서 필요한 축만 고쳐 쓴다.
//
// 스택 방식(Exclusive), 전체 화면, 캐시 정책 축은 그것을 쓰는 첫 창을 이관할 때 추가한다.
// 그때까지 창은 전부 스택에 쌓이고(Overlay) 인스턴스는 재사용된다.
public struct WindowOptions
{
    public UISortBand Band;

    // 아래 화면을 어둡게 하고 입력을 막는다.
    public bool Modal;

    // 모달 바깥(딤)을 누르면 닫는다.
    public bool CloseOnOutside;

    // 뒤로가기로 닫는다. 꺼도 뒤로가기는 이 창이 먹는다(밑의 창으로 넘어가지 않는다).
    public bool CloseOnBack;

    public static WindowOptions ModalPopup => new WindowOptions
    {
        Band = UISortBand.Popup,
        Modal = true,
        CloseOnOutside = true,
        CloseOnBack = true,
    };
}
