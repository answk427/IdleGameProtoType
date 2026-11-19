using UnityEngine;

// 패널이 올라갈 Canvas 레이어. 패널 종류가 정하며 인스펙터에서 고르지 않는다.
public enum UILayer
{
    Hud,
    Panel,
    Top
}

// UIManager의 스택에 참여하는 방식. 패널 종류가 선언하고 UIManager는 이 값만 본다.
public enum StackMode
{
    None,       // 스택 밖 (HUD, 잠깐 뜨는 안내)
    Overlay,    // 스택 위에 쌓임 (팝업)
    Exclusive   // 스택을 비우고 자기가 바닥이 됨 (탭)
}

// 하단 탭바와 연동되는 UI들의 식별자.
public enum UITabType
{
    None,
    Character,
    Skill,
    Equipment,
    Companion,
    Adventure,
    Shop
}

// 모든 UI 패널의 베이스. 패널 종류(UIHud/UITabPanel/UIPopup/UITransient)가 Layer와 StackMode를 정한다.
// Show/Hide는 멱등이다 — 이미 켜진 패널에 Show를 다시 불러도 OnShown은 한 번만 실행된다.
// 이벤트 구독 같은 켜질 때/꺼질 때 작업은 Show/Hide를 오버라이드하지 말고 OnShown/OnHidden에 둔다.
public abstract class UIBase : MonoBehaviour
{
    public virtual UILayer Layer => UILayer.Panel;
    public virtual StackMode StackMode => StackMode.None;

    public bool IsShown { get; private set; }

    // UIManager가 패널을 등록한 직후 한 번 호출한다. 꺼진 상태에서도 받아야 하는
    // 이벤트(예: 스테이지 변경 시 자동 표시)는 여기서 구독한다.
    public virtual void Initialize() { }

    // 스택에 참여하는 패널(Overlay/Exclusive)은 UIManager.ShowUI/HideUI로 열고 닫는다.
    // 직접 부르면 스택과 어긋난다.
    public void Show()
    {
        if (IsShown)
        {
            OnReshown();
            return;
        }

        IsShown = true;
        gameObject.SetActive(true);
        OnShown();
    }

    public void Hide()
    {
        bool wasShown = IsShown;
        IsShown = false;

        if (wasShown) OnHidden();
        gameObject.SetActive(false);
    }

    protected virtual void OnShown() { }
    protected virtual void OnHidden() { }

    // 이미 켜져 있는 패널에 Show가 다시 불렸을 때. 기본은 무시.
    protected virtual void OnReshown() { }

    protected virtual void OnDestroy() { }
}

// 상시 표시되는 HUD. 게임이 시작되면 자동으로 켜진다.
public abstract class UIHud : UIBase
{
    public override UILayer Layer => UILayer.Hud;

    // 게임 시작 시 자동으로 켤지. 조건부로 뜨는 HUD(예: 보스 도전 버튼)는 false로 오버라이드한다.
    protected virtual bool ShowOnGameStart => true;

    public override void Initialize()
    {
        if (ShowOnGameStart) GlobalGameEvents.OnGameStarted += Show;
    }

    protected override void OnDestroy()
    {
        GlobalGameEvents.OnGameStarted -= Show;
    }
}

// 탭 하나만 선택되는 패널. 열면 스택을 비우고 자기가 바닥이 된다.
public abstract class UITabPanel : UIBase
{
    public override StackMode StackMode => StackMode.Exclusive;

    [Header("Tab 연동")]
    public UITabType TabType = UITabType.None;
}

// 위에 겹쳐 뜨는 창. 스택에 쌓이고 뒤로가기로 위에서부터 닫힌다.
public abstract class UIPopup : UIBase
{
    public override StackMode StackMode => StackMode.Overlay;
}

// 잠깐 떴다가 스스로 사라지는 안내. 스택 밖, 최상단 레이어.
public abstract class UITransient : UIBase
{
    public override UILayer Layer => UILayer.Top;
}
