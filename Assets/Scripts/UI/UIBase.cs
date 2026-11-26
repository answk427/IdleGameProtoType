using UnityEngine;

// UIManager의 스택에 참여하는 방식. 패널 종류가 선언하고 UIManager는 이 값만 본다.
public enum StackMode
{
    None,       // 스택 밖(지금은 쓰는 패널이 없다. HUD와 안내는 View + Presenter가 됐다)
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

// 기존 UIManager가 관리하는 패널의 베이스(팝업은 UIWindow). 지금 남은 것은 탭(UITabPanel)뿐이고,
// 탭 이관(docs/TODO.md 다음 작업 6)에서 UIManager와 함께 없어진다. HUD·보스 버튼·스테이지 안내는 전투 4단계에서 View + Presenter가 됐다.
// Show/Hide는 멱등이다 — 이미 켜진 패널에 Show를 다시 불러도 OnShown은 한 번만 실행된다.
// 이벤트 구독 같은 켜질 때/꺼질 때 작업은 Show/Hide를 오버라이드하지 말고 OnShown/OnHidden에 둔다.
public abstract class UIBase : MonoBehaviour
{
    public virtual StackMode StackMode => StackMode.None;

    public bool IsShown { get; private set; }

    // UIManager가 패널을 등록한 직후 한 번 호출한다.
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

// 탭 하나만 선택되는 패널. 열면 스택을 비우고 자기가 바닥이 된다.
public abstract class UITabPanel : UIBase
{
    public override StackMode StackMode => StackMode.Exclusive;

    [Header("Tab 연동")]
    public UITabType TabType = UITabType.None;
}
