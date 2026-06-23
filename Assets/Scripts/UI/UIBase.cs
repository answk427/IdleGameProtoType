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

    [Header("Tab 연동 (탭 UI가 아니면 None)")]
    public UITabType TabType = UITabType.None;

    public virtual void Show()
    {
        gameObject.SetActive(true);
    }

    public virtual void Hide()
    {
        gameObject.SetActive(false);
    }
}
