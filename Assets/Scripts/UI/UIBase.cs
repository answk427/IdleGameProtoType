using UnityEngine;

public enum UILayer
{
    Static,
    Dynamic,
    Top
}

// 하단 탭바와 연동되는 UI들의 식별자.
// None이면 탭과 무관한 일반 UI(HUD, 알림 등)로 취급되어 탭 전환에 영향받지 않음.
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

public abstract class UIBase : MonoBehaviour
{
    [Header("UI Layer")]
    public UILayer Layer = UILayer.Static;

    public virtual void Show()
    {
        gameObject.SetActive(true);
    }

    public virtual void Hide()
    {
        gameObject.SetActive(false);
    }
}

// 하단 탭바와 연동되는, 서로 배타적으로 전환되는 UI 전용 베이스.
// 탭과 무관한 일반 UI(HUD, 안내 토스트, 1회성 버튼 등)는 UIBase를 그대로 쓰고
// 이 클래스를 상속하지 않는다.
public abstract class UITabPanel : UIBase
{
    [Header("Tab 연동")]
    public UITabType TabType = UITabType.None;
}
