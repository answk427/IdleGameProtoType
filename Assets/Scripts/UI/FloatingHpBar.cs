using UnityEngine;
using UnityEngine.UI;

public class FloatingHpBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    private IHasHp myOwner;

    private void Awake()
    {
        myOwner = GetComponentInParent<IHasHp>();

        if (myOwner == null)
        {
            Debug.LogError("부모 중에 체력(IHasHp)을 가진 오브젝트가 없음.");
        }
    }

    private void OnEnable()
    {
        if (myOwner != null) myOwner.OnHpChanged += UpdateHpBar;
    }

    private void OnDisable()
    {
        if (myOwner != null) myOwner.OnHpChanged -= UpdateHpBar;
    }

    public void UpdateHpBar(float currentHp, float maxHp)
    {
        if (fillImage != null && maxHp > 0)
        {
            fillImage.fillAmount = currentHp / maxHp;
        }
    }
}