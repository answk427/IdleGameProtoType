using System.Collections.Generic;
using UnityEngine;

// 스킬 관리 팝업
// 목록에서 배운 스킬을 한 번 클릭(선택) → 상단 슬롯을 클릭하면 그 슬롯에 배치
// 미학습 스킬은 더블클릭으로 학습 시도 (레벨 조건만 체크).
public class SkillManagePopup : UIBase
{
    [SerializeField] private Transform slotContainer;
    [SerializeField] private SkillSlotWidget slotWidgetPrefab;
    [SerializeField] private Transform listContent;       // ScrollView Content
    [SerializeField] private SkillListItem listItemPrefab;
    [SerializeField] private UnityEngine.UI.Button closeButton;

    private readonly List<SkillSlotWidget> slotWidgets = new List<SkillSlotWidget>();
    private PlayerController player;
    private readonly List<SkillListItem> spawnedItems = new List<SkillListItem>();
    private int selectedSkillId = -1;

    private void Awake()
    {
        BuildSlots();

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() => UIManager.Instance.HideUI<SkillManagePopup>());
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
            Debug.LogError("[SkillManagePopup] Player를 찾을 수 없습니다.");
            return;
        }

        player.Stats.OnSkillLearned += RefreshList;
        player.Stats.OnSkillEquipChanged += RefreshAll;

        selectedSkillId = -1;
        BuildList();
        RefreshSlots();
    }

    public override void Hide()
    {
        if (player != null)
        {
            player.Stats.OnSkillLearned -= RefreshList;
            player.Stats.OnSkillEquipChanged -= RefreshAll;
        }
        selectedSkillId = -1;
        base.Hide();
    }

    private void RefreshAll()
    {
        RefreshList();
        RefreshSlots();
    }

    // PlayerSaveData.SkillSlotCount 개수만큼 슬롯 위젯을 런타임에 생성한다.
    private void BuildSlots()
    {
        if (slotContainer == null || slotWidgetPrefab == null) return;

        for (int i = 0; i < PlayerSaveData.SkillSlotCount; i++)
        {
            SkillSlotWidget widget = Instantiate(slotWidgetPrefab, slotContainer);
            widget.Initialize(i, OnSlotWidgetClicked);
            slotWidgets.Add(widget);
        }
    }

    // ── 하단 목록 ──

    private void BuildList()
    {
        // Destroy()는 프레임 종료 시점까지 제거를 미루기 때문에, Show()가 짧은 간격으로
        // 다시 호출되면 이전 목록이 채 사라지기 전에 새 목록이 또 생겨 항목이 중복될 수 있다.
        // 즉시 제거를 보장하기 위해 DestroyImmediate를 사용한다.
        foreach (var item in spawnedItems)
        {
            if (item != null) DestroyImmediate(item.gameObject);
        }
        spawnedItems.Clear();

        if (GameDatabaseManager.Instance == null || listContent == null || listItemPrefab == null) return;

        foreach (SkillEntry entry in GameDatabaseManager.Instance.GetAllSkills())
        {
            if (entry?.data == null) continue;

            SkillListItem item = Instantiate(listItemPrefab, listContent);
            bool isLearned = player.Stats.IsSkillLearned(entry.data.Id);
            item.Bind(entry, isLearned, OnListItemSingleClick, OnListItemDoubleClick);
            spawnedItems.Add(item);
        }
    }

    private void RefreshList()
    {
        if (GameDatabaseManager.Instance == null) return;

        foreach (var item in spawnedItems)
        {
            SkillEntry entry = GameDatabaseManager.Instance.GetSkill(item.SkillId);
            if (entry?.data == null) continue;
            bool isLearned = player.Stats.IsSkillLearned(entry.data.Id);
            item.Bind(entry, isLearned, OnListItemSingleClick, OnListItemDoubleClick);
        }
        ApplySelectionHighlight();
    }

    private void OnListItemSingleClick(int skillId)
    {
        if (!player.Stats.IsSkillLearned(skillId)) return; // 배우지 않은 스킬은 배치 선택 불가
        selectedSkillId = (selectedSkillId == skillId) ? -1 : skillId; // 같은 항목 재클릭 시 선택 해제
        ApplySelectionHighlight();
    }

    private void OnListItemDoubleClick(int skillId)
    {
        if (player.Stats.CanLearnSkill(skillId))
        {
            player.Stats.LearnSkill(skillId);
        }
    }

    private void ApplySelectionHighlight()
    {
        foreach (var item in spawnedItems)
        {
            item.SetSelected(item.SkillId == selectedSkillId);
        }
    }

    // ── 상단 슬롯 ──

    private void RefreshSlots()
    {
        for (int i = 0; i < slotWidgets.Count; i++)
        {
            Skill skill = player.GetEquippedSkill(i);
            if (skill == null) slotWidgets[i].SetEmpty();
            else slotWidgets[i].SetSkill(skill.Entry.icon, skill.CooldownRatio);
        }
    }

    private void OnSlotWidgetClicked(int slotIndex)
    {
        if (player == null) return;

        if (selectedSkillId >= 0)
        {
            // 1단계에서 목록에서 스킬을 선택한 상태 → 이 슬롯에 배치
            player.Stats.EquipSkill(selectedSkillId, slotIndex);
            selectedSkillId = -1;
            ApplySelectionHighlight();
        }
        else
        {
            // 선택된 스킬이 없으면 해당 슬롯을 비움
            player.Stats.UnequipSkill(slotIndex);
        }
        RefreshSlots();
    }
}
