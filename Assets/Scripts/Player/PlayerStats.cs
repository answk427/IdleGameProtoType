using System;
using System.Collections.Generic;
using UnityEngine;

public enum UpgradeStatType
{
    Hp,
    Attack,
    AttackSpeed
}

// 최종 스탯 = 레벨 기본 스탯 (PlayerStatDatabase) + 업그레이드 보너스 (PlayerSaveData)
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
    private PlayerSaveData saveData;

    // 고정 이동속도. 버프/스킬로만 일시적으로 바뀐다
    private float baseRunSpeed;

    // ── 읽기 전용 프로퍼티 ──────────────────────────────────

    public int Level => saveData?.level ?? 1;
    public int CurrentExp => saveData?.currentExp ?? 0;
    public int RequiredExp => statData?.GetRequiredExp(Level) ?? 999;
    public bool IsMaxLevel => Level >= (statData?.MaxLevel ?? 1);

    // 최종 스탯 = 기본 + 업그레이드 보너스
    public int MaxHp => BaseStat.BaseMaxHp + (saveData?.hpUpgradeLevel ?? 0) * upgradeConfig.settings.hpPerUpgrade;
    public int AttackDamage => BaseStat.BaseAttackDamage + (saveData?.attackUpgradeLevel ?? 0) * upgradeConfig.settings.attackPerUpgrade;
    public float AttackInterval => Mathf.Max(0.1f, BaseStat.BaseAttackInterval - (saveData?.attackSpeedUpgradeLevel ?? 0) * upgradeConfig.settings.attackSpeedPerUpgrade);

    // 레벨/업그레이드의 영향을 받지 않는 고정값. 버프/스킬이 생기면 여기에 배율만 곱하면 된다.
    public float RunSpeed => baseRunSpeed;

    private PlayerStatData BaseStat => statData.GetByLevel(Level);

    // 스탯 종류별 동작(레벨 읽기/올리기, 비용, 현재·다음 값)을 한 곳에 등록.
    // 새 스탯을 추가하려면 IUpgradeStat 구현 클래스 하나 만들고 여기에 등록만 하면 된다.
    private Dictionary<UpgradeStatType, IUpgradeStat> upgradeStats;

    // ── 초기화 ──────────────────────────────────────────────

    public void Initialize(PlayerStatDatabase statData, PlayerUpgradeConfig upgradeConfig, float baseRunSpeed)
    {
        this.statData = statData;
        this.upgradeConfig = upgradeConfig;
        this.baseRunSpeed = baseRunSpeed;
        this.saveData = new PlayerSaveData(); // 기본값

        upgradeStats = new Dictionary<UpgradeStatType, IUpgradeStat>
        {
            [UpgradeStatType.Hp] = new HpUpgradeStat(this, upgradeConfig),
            [UpgradeStatType.Attack] = new AttackUpgradeStat(this, upgradeConfig),
            [UpgradeStatType.AttackSpeed] = new AttackSpeedUpgradeStat(this, upgradeConfig),
        };
    }

    public void LoadSave(PlayerSaveData data)
    {
        saveData = data;
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

    // 버튼 클릭 등으로 호출. 밀린 경험치가 여러 레벨 분량이면 한 번에 다 처리한다.
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
    // 스탯 종류별 실제 동작은 upgradeStats에 등록된 IUpgradeStat 구현체가 담당.

    public int GetCurrentUpgradeLevel(UpgradeStatType type) => upgradeStats[type].GetLevel();

    public int GetNextUpgradeCost(UpgradeStatType type)
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

    public bool IsSkillLearned(int skillId) => saveData?.learnedSkillIds.Contains(skillId) ?? false;

    public IReadOnlyList<int> LearnedSkillIds => saveData?.learnedSkillIds;

    public bool CanLearnSkill(int skillId)
    {
        if (IsSkillLearned(skillId)) return false;

        SkillEntry entry = GameDatabaseManager.Instance?.GetSkill(skillId);
        if (entry?.data == null) return false;

        return Level >= entry.data.RequiredLevel;
    }

    public bool LearnSkill(int skillId)
    {
        if (!CanLearnSkill(skillId)) return false;

        saveData.learnedSkillIds.Add(skillId);
        Save();
        OnSkillLearned?.Invoke();
        Debug.Log($"[PlayerStats] 스킬 학습: {skillId}");
        return true;
    }

    public int GetEquippedSkillId(int slotIndex)
    {
        if (saveData == null || slotIndex < 0 || slotIndex >= saveData.equippedSkillSlotIds.Length)
            return PlayerSaveData.EmptySlot;
        return saveData.equippedSkillSlotIds[slotIndex];
    }

    public bool EquipSkill(int skillId, int slotIndex)
    {
        if (saveData == null || slotIndex < 0 || slotIndex >= saveData.equippedSkillSlotIds.Length) return false;
        if (!IsSkillLearned(skillId)) return false;

        // 다른 슬롯에 이미 배치되어 있던 스킬이면 그 슬롯은 비운다 (중복 배치 방지)
        for (int i = 0; i < saveData.equippedSkillSlotIds.Length; i++)
        {
            if (saveData.equippedSkillSlotIds[i] == skillId)
                saveData.equippedSkillSlotIds[i] = PlayerSaveData.EmptySlot;
        }

        saveData.equippedSkillSlotIds[slotIndex] = skillId;
        Save();
        OnSkillEquipChanged?.Invoke();
        return true;
    }

    public bool UnequipSkill(int slotIndex)
    {
        if (saveData == null || slotIndex < 0 || slotIndex >= saveData.equippedSkillSlotIds.Length) return false;
        if (saveData.equippedSkillSlotIds[slotIndex] == PlayerSaveData.EmptySlot) return false;

        saveData.equippedSkillSlotIds[slotIndex] = PlayerSaveData.EmptySlot;
        Save();
        OnSkillEquipChanged?.Invoke();
        return true;
    }

    // ── 저장 ────────────────────────────────────────────────

    public void Save() => saveData?.Save();
    public PlayerSaveData GetSaveData() => saveData;
}
