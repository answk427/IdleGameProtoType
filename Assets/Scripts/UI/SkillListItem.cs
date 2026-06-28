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

    // 모바일 터치는 Unity EventSystem이 clickCount를 마우스처럼 1->2로 못 올려주는 경우가 많아서
    // (PC에서는 더블클릭 인식되는데 모바일에서는 항상 1로만 들어옴), 직접 시간차로 더블탭을 판정한다.
    private const float DoubleClickThreshold = 0.3f;
    private static int lastClickedSkillId = -1;
    private static float lastClickTime = -999f;

    public void OnPointerClick(PointerEventData eventData)
    {
        bool isDoubleClick = SkillId == lastClickedSkillId && Time.unscaledTime - lastClickTime <= DoubleClickThreshold;
        lastClickedSkillId = SkillId;
        lastClickTime = Time.unscaledTime;

        if (isDoubleClick)
        {
            onDoubleClick?.Invoke(SkillId);
            lastClickedSkillId = -1;
        }
        else
        {
            onSingleClick?.Invoke(SkillId);
        }
    }
}
