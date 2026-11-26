using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스킬 관리 창 하단 목록의 항목 1개. 표시와 탭 전달만 한다(더블탭 판정은 SkillManageWindow).
// 탭은 같은 오브젝트의 UIButton으로 받는다(모든 UI 클릭이 한 프레임 하나 규칙을 지나게, docs/UI_DESIGN.md 10-6).
[RequireComponent(typeof(UIButton))]
public class SkillListItem : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI requiredLevelText;
    [SerializeField] private Image selectedHighlight; // 선택 시 테두리 등 강조
    [SerializeField] private GameObject learnableBadge; // 레드닷: 지금 배울 수 있다

    private static readonly Color LearnedColor = Color.white;
    private static readonly Color UnlearnedColor = Color.gray;

    public int SkillId { get; private set; }

    private Action<int> onTapped;

    private void Awake()
    {
        GetComponent<UIButton>().onClick.AddListener(() => onTapped?.Invoke(SkillId));
    }

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

    public void SetLearnable(bool learnable)
    {
        if (learnableBadge != null) learnableBadge.SetActive(learnable);
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null) selectedHighlight.enabled = selected;
    }
}
