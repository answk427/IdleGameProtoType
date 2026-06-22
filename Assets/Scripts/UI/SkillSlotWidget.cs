using System;
using UnityEngine;
using UnityEngine.UI;

// 스킬 슬롯 1칸의 표시(아이콘/쿨다운)와 클릭 입력을 담당.
// 전투 탭의 SkillSlotBar와 SkillManagePopup 상단 영역에서 공통으로 재사용한다.
public class SkillSlotWidget : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image cooldownOverlay; // fillAmount로 쿨다운 비율 표시 (Image Type: Filled)

    private int slotIndex;
    private Action<int> onClick;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(() => onClick?.Invoke(slotIndex));
        }
    }

    public void Initialize(int index, Action<int> onClickCallback)
    {
        slotIndex = index;
        onClick = onClickCallback;
    }

    public void SetEmpty()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
        if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
    }

    public void SetSkill(Sprite icon, float cooldownRatio)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = true;
        }
        if (cooldownOverlay != null) cooldownOverlay.fillAmount = Mathf.Clamp01(cooldownRatio);
    }
}
