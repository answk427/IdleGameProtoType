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
