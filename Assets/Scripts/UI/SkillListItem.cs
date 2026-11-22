using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 스킬 관리 창 하단 목록의 항목 1개. 표시와 탭 전달만 한다(더블탭 판정은 SkillManageWindow).
public class SkillListItem : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI requiredLevelText;
    [SerializeField] private Image selectedHighlight; // 선택 시 테두리 등 강조

    private static readonly Color LearnedColor = Color.white;
    private static readonly Color UnlearnedColor = Color.gray;

    public int SkillId { get; private set; }

    private Action<int> onTapped;

    public void Bind(int skillId, Sprite icon, string displayName, string requirement, Action<int> tapped)
    {
        SkillId = skillId;
        onTapped = tapped;

        if (iconImage != null) iconImage.sprite = icon;
        if (nameText != null) nameText.text = displayName;
        if (requiredLevelText != null) requiredLevelText.text = requirement;

        SetSelected(false);
    }

    public void SetLearned(bool learned)
    {
        if (iconImage != null) iconImage.color = learned ? LearnedColor : UnlearnedColor;
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null) selectedHighlight.enabled = selected;
    }

    public void OnPointerClick(PointerEventData eventData) => onTapped?.Invoke(SkillId);
}
