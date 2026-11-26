using System;
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
    private IDisposable equipSubscription;

    // 어떤 창을 어떻게 열지는 모른다. 요청만 내고 UIRoot가 연다.
    public event Action SkillManageRequested;

    private void Awake()
    {
        BuildSlots();

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(() => SkillManageRequested?.Invoke());
        }
    }

    // PlayerStatsSaveData.SkillSlotCount 개수만큼 슬롯 위젯을 런타임에 생성한다.
    private void BuildSlots()
    {
        if (slotContainer == null || slotWidgetPrefab == null) return;

        for (int i = 0; i < PlayerStatsSaveData.SkillSlotCount; i++)
        {
            SkillSlotWidget widget = Instantiate(slotWidgetPrefab, slotContainer);
            widget.Initialize(i, OnSlotClicked);
            slotWidgets.Add(widget);
        }
    }

    protected override void OnShown()
    {
        if (player == null && GameManager.Instance != null)
        {
            player = GameManager.Instance.GetPlayer();
        }

        if (player == null)
        {
            Debug.LogError("[SkillSlotBar] Player를 찾을 수 없습니다.");
            return;
        }

        // 장착 슬롯 상태를 구독한다(구독하는 순간 지금 값으로 그린다). 캐릭터가 먼저 구독돼 있어서
        // 알림을 받을 때 캐릭터의 장착 스킬은 이미 새 값이다. 창 이관(다음 작업 6)에서 Presenter로 옮긴다.
        equipSubscription?.Dispose();
        equipSubscription = player.Stats.EquippedSlots.Subscribe(_ => RefreshAll());
    }

    protected override void OnHidden()
    {
        equipSubscription?.Dispose();
        equipSubscription = null;
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
                slotWidgets[i].SetSkill(skill.Entry.icon, player.GetSkillCooldownRatio(i));
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
                slotWidgets[i].SetSkill(skill.Entry.icon, player.GetSkillCooldownRatio(i));
            }
        }
    }

    private void OnSlotClicked(int slotIndex)
    {
        if (player == null) return;
        player.RequestSkillSlot(slotIndex);
    }
}
