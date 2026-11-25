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

// 기존 UIManager가 관리하는 패널의 베이스(팝업은 UIWindow). 패널 종류(UIHud/UITabPanel/UITransient)가 Layer와 StackMode를 정한다.
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

    // 스택에 참여하는 패널(Exclusive)은 UIManager.ShowUI/HideUI로 열고 닫는다.
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

// 상시 표시되는 HUD. 조립이 끝나면 UIRoot가 켠다(이관 기간 연결). 예전의 "게임 시작" 사건(OnGameStarted)은
// GameInitializer가 UI를 조립한 뒤에 흐름을 시작해서 필요 없어졌다(docs/COMBAT_ARCHITECTURE.md 10-5).
// 4-B에서 PlayerHud가 View + Presenter가 되면 이 클래스는 없어진다.
public abstract class UIHud : UIBase
{
    public override UILayer Layer => UILayer.Hud;
}

// 탭 하나만 선택되는 패널. 열면 스택을 비우고 자기가 바닥이 된다.
public abstract class UITabPanel : UIBase
{
    public override StackMode StackMode => StackMode.Exclusive;

    [Header("Tab 연동")]
    public UITabType TabType = UITabType.None;
}

// 잠깐 떴다가 스스로 사라지는 안내. 스택 밖, 최상단 레이어.
public abstract class UITransient : UIBase
{
    public override UILayer Layer => UILayer.Top;
}
