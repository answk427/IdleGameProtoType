using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 전투 탭: 5개의 스킬 슬롯을 표시한다. 장착된 스킬은 준비되는 즉시 자동으로 사용되고,
// 슬롯을 클릭하면 그 자리에서 바로 사용할 수도 있다(PlayerController.TryUseSkillSlot이 IsReady만 체크).
public class SkillSlotBar : UIBase
{
    [SerializeField] private Transform slotContainer;
    [SerializeField] private SkillSlotWidget slotWidgetPrefab;
    [SerializeField] private Button settingsButton;

    private readonly List<SkillSlotWidget> slotWidgets = new List<SkillSlotWidget>();
    private PlayerController player;

    private void Awake()
    {
        BuildSlots();

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(() => UIManager.Instance.ShowUI<SkillManagePopup>());
        }
    }

    // PlayerSaveData.SkillSlotCount 개수만큼 슬롯 위젯을 런타임에 생성한다.
    // 슬롯 개수가 바뀌어도 씬에서 수동으로 맞춰줄 필요가 없다.
    private void BuildSlots()
    {
        if (slotContainer == null || slotWidgetPrefab == null) return;

        for (int i = 0; i < PlayerSaveData.SkillSlotCount; i++)
        {
            SkillSlotWidget widget = Instantiate(slotWidgetPrefab, slotContainer);
            widget.Initialize(i, OnSlotClicked);
            slotWidgets.Add(widget);
        }
    }

    public override void Show()
    {
        base.Show();

        if (player == null && GameManager.Instance != null)
        {
            player = GameManager.Instance.GetPlayer();
        }

        if (player == null)
        {
            Debug.LogError("[SkillSlotBar] Player를 찾을 수 없습니다.");
            return;
        }

        player.Stats.OnSkillEquipChanged += RefreshAll;

        RefreshAll();
    }

    public override void Hide()
    {
        if (player != null)
        {
            player.Stats.OnSkillEquipChanged -= RefreshAll;
        }
        base.Hide();
    }

    private void Update()
    {
        if (!gameObject.activeInHierarchy || player == null) return;
        RefreshCooldowns();
    }

    private void RefreshAll()
    {
        if (player == null) return;

        for (int i = 0; i < slotWidgets.Count; i++)
        {
            Skill skill = player.GetEquippedSkill(i);
            if (skill == null)
            {
                slotWidgets[i].SetEmpty();
            }
            else
            {
                slotWidgets[i].SetSkill(skill.Entry.icon, skill.CooldownRatio);
            }
        }
    }

    private void RefreshCooldowns()
    {
        for (int i = 0; i < slotWidgets.Count; i++)
        {
            Skill skill = player.GetEquippedSkill(i);
            if (skill != null)
            {
                slotWidgets[i].SetSkill(skill.Entry.icon, skill.CooldownRatio);
            }
        }
    }

    private void OnSlotClicked(int slotIndex)
    {
        if (player == null) return;
        player.TryUseSkillSlot(slotIndex);
    }
}
