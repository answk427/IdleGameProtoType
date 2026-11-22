using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum UpgradeStatType
{
    Hp,
    Attack,
    AttackSpeed
}

public enum LearnSkillResult
{
    Success,
    AlreadyLearned,
    UnknownSkill,
    LevelTooLow,
}

public enum SkillSlotResult
{
    Success,
    Unchanged,
    NotLearned,
    InvalidSlot,
}

// 최종 스탯 = 레벨 기본 스탯 (PlayerStatDatabase) + 업그레이드 보너스 (PlayerStatsSaveData)
[System.Serializable]
public class PlayerStats
{
    // ── 이벤트 ──────────────────────────────────────────────
    public event Action<int> OnLevelUp;           // 레벨업 시 (새 레벨 전달)
    public event Action<int, int> OnExpChanged;   // 경험치 변화 시 (현재, 필요)
    public event Action OnUpgraded;               // 업그레이드 구매 시
    public event Action OnSkillLearned;           // 스킬 학습 시
    public event Action OnSkillEquipChanged;      // 스킬 슬롯 배치 변경 시

    // ── 참조 ────────────────────────────────────────────────
    private PlayerStatDatabase statData;
    private PlayerUpgradeConfig upgradeConfig;
    private PlayerStatsSaveData saveData;

    // ── 상태 ────────────────────────────────────────────────
    // 모음은 내용을 고치지 않고 새 목록으로 교체한다. 바꾸는 곳은 SetLearnedSkills/SetEquippedSlots뿐이다.
    private readonly ReactiveProperty<IReadOnlyList<int>> learnedSkills =
        new ReactiveProperty<IReadOnlyList<int>>(Array.Empty<int>());
    private readonly ReactiveProperty<IReadOnlyList<int>> equippedSlots =
        new ReactiveProperty<IReadOnlyList<int>>(Array.AsReadOnly(new PlayerStatsSaveData().equippedSkillSlotIds));

    public IReadOnlyReactiveProperty<IReadOnlyList<int>> LearnedSkills => learnedSkills;
    public IReadOnlyReactiveProperty<IReadOnlyList<int>> EquippedSlots => equippedSlots;

    // ── 읽기 전용 프로퍼티 ──────────────────────────────────

    public int Level => saveData?.level ?? 1;
    public int CurrentExp => saveData?.currentExp ?? 0;
    public int RequiredExp => statData?.GetRequiredExp(Level) ?? 999;
    public bool IsMaxLevel => Level >= (statData?.MaxLevel ?? 1);

    // 최종 스탯 = 기본 + 업그레이드 보너스
    public int MaxHp => BaseStat.BaseMaxHp + (saveData?.hpUpgradeLevel ?? 0) * upgradeConfig.settings.hpPerUpgrade;
    public int AttackDamage => BaseStat.BaseAttackDamage + (saveData?.attackUpgradeLevel ?? 0) * upgradeConfig.settings.attackPerUpgrade;
    public float AttackInterval => Mathf.Max(0.1f, BaseStat.BaseAttackInterval - (saveData?.attackSpeedUpgradeLevel ?? 0) * upgradeConfig.settings.attackSpeedPerUpgrade);

    private PlayerStatData BaseStat => statData.GetByLevel(Level);

    // 스탯 종류별 동작(IUpgradeStat)을 한 곳에 등록.
    // 새 스탯을 추가하려면 IUpgradeStat 구현 클래스 하나 만들고 여기에 등록만 하면 된다.
    private Dictionary<UpgradeStatType, IUpgradeStat> upgradeStats;

    // ── 초기화 ──────────────────────────────────────────────

    // 이동속도처럼 저장되지 않는 캐릭터 속성은 여기(진행도)가 아니라 PlayerController가 갖는다.
    public void Initialize(PlayerStatDatabase statData, PlayerUpgradeConfig upgradeConfig)
    {
        this.statData = statData;
        this.upgradeConfig = upgradeConfig;
        LoadSave(null); // 기본값

        IUpgradeStat[] stats = {
            new HpUpgradeStat(this, upgradeConfig),
            new AttackUpgradeStat(this, upgradeConfig),
            new AttackSpeedUpgradeStat(this, upgradeConfig),
        };

        upgradeStats = new Dictionary<UpgradeStatType, IUpgradeStat>();
        foreach (IUpgradeStat stat in stats)
        {
            upgradeStats[stat.Type] = stat;
        }
    }

    // GameSession.Apply()가 호출한다. PlayerStats 객체 자체는 그대로 두고 데이터만 갈아끼우므로,
    // 이 객체를 참조하는 쪽(캐릭터, UI)은 다시 연결할 필요가 없다.
    public void LoadSave(PlayerStatsSaveData data)
    {
        saveData = data ?? new PlayerStatsSaveData();

        // 상태만 갱신한다. 저장을 일으키는 사건(OnSkillLearned 등)은 내지 않는다.
        SetLearnedSkills(saveData.learnedSkillIds ?? new List<int>());
        SetEquippedSlots(saveData.equippedSkillSlotIds ?? new PlayerStatsSaveData().equippedSkillSlotIds);
    }

    // ── 경험치 / 레벨업 ─────────────────────────────────────
    // 경험치는 쌓이기만 하고, 레벨업은 TryLevelUp()을 명시적으로 호출해야 일어난다
    // (캐릭터 탭의 "LEVEL UP!" 버튼이 호출).

    public bool CanLevelUp => !IsMaxLevel && saveData != null && saveData.currentExp >= RequiredExp;

    public void AddExp(int amount)
    {
        if (IsMaxLevel) return;

        saveData.currentExp += amount;
        OnExpChanged?.Invoke(saveData.currentExp, RequiredExp);
    }

    // 밀린 경험치가 여러 레벨 분량이면 한 번에 다 처리한다.
    public bool TryLevelUp()
    {
        if (!CanLevelUp) return false;

        while (!IsMaxLevel && saveData.currentExp >= RequiredExp)
        {
            saveData.currentExp -= RequiredExp;
            saveData.level++;
            OnLevelUp?.Invoke(saveData.level);
            OnExpChanged?.Invoke(saveData.currentExp, RequiredExp);
            Debug.Log($"[PlayerStats] 레벨업! → Lv.{saveData.level} | HP:{MaxHp} ATK:{AttackDamage}");
        }

        return true;
    }

    // ── 업그레이드 (돈은 외부에서 검사) ────────
    public int GetCurrentUpgradeLevel(UpgradeStatType type) => upgradeStats[type].GetLevel();

    public GameNumber GetNextUpgradeCost(UpgradeStatType type)
    {
        IUpgradeStat stat = upgradeStats[type];
        return stat.GetCost(stat.GetLevel());
    }

    public bool IsUpgradeMaxed(UpgradeStatType type)
    {
        if (upgradeConfig.maxUpgradeLevel <= 0) return false;
        return GetCurrentUpgradeLevel(type) >= upgradeConfig.maxUpgradeLevel;
    }

    public float GetCurrentStatValue(UpgradeStatType type) => upgradeStats[type].GetCurrentValue();

    // 한 번 더 업그레이드했을 때의 값. UI에서 "현재 → 다음" 표시용.
    public float GetNextStatValue(UpgradeStatType type) => upgradeStats[type].GetNextValue();

    public void Upgrade(UpgradeStatType type)
    {
        if (IsUpgradeMaxed(type))
        {
            Debug.LogWarning($"[PlayerStats] {type} 최대 업그레이드 도달");
            return;
        }

        upgradeStats[type].IncrementLevel();

        OnUpgraded?.Invoke();
        Debug.Log($"[PlayerStats] {type} 업그레이드 → Lv.{GetCurrentUpgradeLevel(type)}, 현재값:{GetCurrentStatValue(type)}");
    }

    // ── 스킬 학습 / 장착 ────────────────────────────────────
    // 학습 조건은 플레이어 레벨만 체크
    // 명령은 동기 + 실패 이유 반환. 상태를 다 바꾼 뒤에 사건을 낸다.
    // 사건(저장, 캐릭터, SkillSlotBar가 구독)은 그쪽 이관이 끝나면 정리한다.

    public bool IsSkillLearned(int skillId) => learnedSkills.Value.Contains(skillId);

    public bool CanLearnSkill(int skillId) => CheckLearnSkill(skillId) == LearnSkillResult.Success;

    public LearnSkillResult LearnSkill(int skillId)
    {
        LearnSkillResult result = CheckLearnSkill(skillId);
        if (result != LearnSkillResult.Success) return result;

        SetLearnedSkills(learnedSkills.Value.Append(skillId));
        OnSkillLearned?.Invoke();
        Debug.Log($"[PlayerStats] 스킬 학습: {skillId}");
        return LearnSkillResult.Success;
    }

    public int GetEquippedSkillId(int slotIndex)
    {
        IReadOnlyList<int> slots = equippedSlots.Value;
        if (slotIndex < 0 || slotIndex >= slots.Count) return PlayerStatsSaveData.EmptySlot;
        return slots[slotIndex];
    }

    public SkillSlotResult EquipSkill(int skillId, int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return SkillSlotResult.InvalidSlot;
        if (!IsSkillLearned(skillId)) return SkillSlotResult.NotLearned;

        // 새 목록은 내용이 같아도 "다른 값"으로 알려지므로, 바뀌지 않는 명령은 여기서 거른다.
        if (equippedSlots.Value[slotIndex] == skillId) return SkillSlotResult.Unchanged;

        int[] slots = equippedSlots.Value.ToArray();

        // 다른 슬롯에 이미 배치되어 있던 스킬이면 그 슬롯은 비운다 (중복 배치 방지)
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == skillId) slots[i] = PlayerStatsSaveData.EmptySlot;
        }

        slots[slotIndex] = skillId;
        SetEquippedSlots(slots);
        OnSkillEquipChanged?.Invoke();
        return SkillSlotResult.Success;
    }

    public SkillSlotResult UnequipSkill(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return SkillSlotResult.InvalidSlot;
        if (equippedSlots.Value[slotIndex] == PlayerStatsSaveData.EmptySlot) return SkillSlotResult.Unchanged;

        int[] slots = equippedSlots.Value.ToArray();
        slots[slotIndex] = PlayerStatsSaveData.EmptySlot;
        SetEquippedSlots(slots);
        OnSkillEquipChanged?.Invoke();
        return SkillSlotResult.Success;
    }

    private LearnSkillResult CheckLearnSkill(int skillId)
    {
        if (IsSkillLearned(skillId)) return LearnSkillResult.AlreadyLearned;

        SkillEntry entry = GameDatabaseManager.Instance?.GetSkill(skillId);
        if (entry?.data == null) return LearnSkillResult.UnknownSkill;

        return Level >= entry.data.RequiredLevel ? LearnSkillResult.Success : LearnSkillResult.LevelTooLow;
    }

    private bool IsValidSlot(int slotIndex) => saveData != null && slotIndex >= 0 && slotIndex < equippedSlots.Value.Count;

    // DTO를 먼저 맞추고 상태를 알린다. 알림을 받은 쪽이 바로 저장해도 새 값이 나간다.
    private void SetLearnedSkills(IEnumerable<int> skillIds)
    {
        int[] next = skillIds.ToArray();
        saveData.learnedSkillIds = new List<int>(next);
        learnedSkills.Value = Array.AsReadOnly(next);
    }

    private void SetEquippedSlots(IEnumerable<int> skillIds)
    {
        int[] next = skillIds.ToArray();
        saveData.equippedSkillSlotIds = (int[])next.Clone();
        equippedSlots.Value = Array.AsReadOnly(next);
    }

    // 스킬 목록/슬롯은 이 객체로 고치지 말 것(상태에 알려지지 않는다). 위 명령을 쓴다.
    public PlayerStatsSaveData GetSaveData() => saveData;
}
