using UnityEngine;

public enum UILayer
{
    Static,
    Dynamic,
    Top
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