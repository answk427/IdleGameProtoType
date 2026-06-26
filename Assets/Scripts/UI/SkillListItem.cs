using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 스킬 관리 팝업 하단 목록의 항목 1개
// 단일 클릭: 배치를 위한 선택. 더블 클릭: 학습 시도.
public class SkillListItem : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI requiredLevelText;
    [SerializeField] private Image selectedHighlight; // 선택 시 테두리 등 강조

    private static readonly Color LearnedColor = Color.white;
    private static readonly Color UnlearnedColor = Color.gray;

    public int SkillId { get; private set; }

    private Action<int> onSingleClick;
    private Action<int> onDoubleClick;

    public void Bind(SkillEntry entry, bool isLearned, Action<int> singleClickCallback, Action<int> doubleClickCallback)
    {
        SkillId = entry.data.Id;
        onSingleClick = singleClickCallback;
        onDoubleClick = doubleClickCallback;

        if (iconImage != null)
        {
            iconImage.sprite = entry.icon;
            iconImage.color = isLearned ? LearnedColor : UnlearnedColor;
        }
        if (nameText != null) nameText.text = entry.data.SkillName;
        if (requiredLevelText != null) requiredLevelText.text = $"Lv.{entry.data.RequiredLevel}";

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null) selectedHighlight.enabled = selected;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount >= 2)
        {
            onDoubleClick?.Invoke(SkillId);
        }
        else
        {
            onSingleClick?.Invoke(SkillId);
        }
    }
}
