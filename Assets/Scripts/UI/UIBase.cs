using UnityEngine;

public enum UILayer
{
    Static,
    Dynamic,
    Top
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

// UI클래스는 UIBase를 상속 받아야 함
// List의 Item처럼 다른 UI에 속하는 클래스는 상속받지 않음
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

public abstract class UITabPanel : UIBase
{
    [Header("Tab 연동")]
    public UITabType TabType = UITabType.None;
}
