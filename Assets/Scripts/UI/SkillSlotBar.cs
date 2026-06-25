using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SkillSlotBar : UITabPanel
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
