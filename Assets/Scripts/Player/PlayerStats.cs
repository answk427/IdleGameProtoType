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
    public int MaxHp => BaseStat.baseMaxHp + (saveData?.hpUpgradeLevel ?? 0) * upgradeConfig.settings.hpPerUpgrade;
    public int AttackDamage => BaseStat.baseAttackDamage + (saveData?.attackUpgradeLevel ?? 0) * upgradeConfig.settings.attackPerUpgrade;
    public float AttackInterval => Mathf.Max(0.1f, BaseStat.baseAttackInterval - (saveData?.attackSpeedUpgradeLevel ?? 0) * upgradeConfig.settings.attackSpeedPerUpgrade);

    // 레벨/업그레이드의 영향을 받지 않는 고정값. 버프/스킬이 생기면 여기에 배율만 곱하면 된다.
    public float RunSpeed => baseRunSpeed;

    private PlayerStatData BaseStat => statData.GetByLevel(Level);

    // ── 초기화 ──────────────────────────────────────────────

    public void Initialize(PlayerStatDatabase statData, PlayerUpgradeConfig upgradeConfig, float baseRunSpeed)
    {
        this.statData = statData;
        this.upgradeConfig = upgradeConfig;
        this.baseRunSpeed = baseRunSpeed;
        this.saveData = new PlayerSaveData(); // 기본값
    }

    public void LoadSave(PlayerSaveData data)
    {
        saveData = data;
    }

    // ── 경험치 / 레벨업 ─────────────────────────────────────

    public void AddExp(int amount)
    {
        if (IsMaxLevel) return;

        saveData.currentExp += amount;
        OnExpChanged?.Invoke(saveData.currentExp, RequiredExp);

        // 연속 레벨업 가능 (경험치가 많이 쌓인 경우)
        while (!IsMaxLevel && saveData.currentExp >= RequiredExp)
        {
            saveData.currentExp -= RequiredExp;
            saveData.level++;
            OnLevelUp?.Invoke(saveData.level);
            OnExpChanged?.Invoke(saveData.currentExp, RequiredExp);
            Debug.Log($"[PlayerStats] 레벨업! → Lv.{saveData.level} | HP:{MaxHp} ATK:{AttackDamage}");
        }
    }

    // ── 업그레이드 (돈은 외부에서 검사) ────────
    // 스탯 종류가 늘어나도 분기 추가 없이 UpgradeStatType 하나로 처리.

    public int GetCurrentUpgradeLevel(UpgradeStatType type)
    {
        switch (type)
        {
            case UpgradeStatType.Hp: return saveData?.hpUpgradeLevel ?? 0;
            case UpgradeStatType.Attack: return saveData?.attackUpgradeLevel ?? 0;
            case UpgradeStatType.AttackSpeed: return saveData?.attackSpeedUpgradeLevel ?? 0;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    public int GetNextUpgradeCost(UpgradeStatType type)
    {
        int level = GetCurrentUpgradeLevel(type);
        switch (type)
        {
            case UpgradeStatType.Hp: return upgradeConfig.GetHpUpgradeCost(level);
            case UpgradeStatType.Attack: return upgradeConfig.GetAttackUpgradeCost(level);
            case UpgradeStatType.AttackSpeed: return upgradeConfig.GetAttackSpeedUpgradeCost(level);
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    public bool IsUpgradeMaxed(UpgradeStatType type)
    {
        if (upgradeConfig.maxUpgradeLevel <= 0) return false;
        return GetCurrentUpgradeLevel(type) >= upgradeConfig.maxUpgradeLevel;
    }

    public float GetCurrentStatValue(UpgradeStatType type)
    {
        switch (type)
        {
            case UpgradeStatType.Hp: return MaxHp;
            case UpgradeStatType.Attack: return AttackDamage;
            // 공격 간격(초)은 작을수록 강해지는 값이라 그대로 보여주면 직관과 반대로 읽힌다.
            // 다른 스탯들과 동일하게 "클수록 좋다"가 되도록 초당 공격 횟수로 환산해서 보여준다.
            case UpgradeStatType.AttackSpeed: return 1f / AttackInterval;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    public void Upgrade(UpgradeStatType type)
    {
        if (IsUpgradeMaxed(type))
        {
            Debug.LogWarning($"[PlayerStats] {type} 최대 업그레이드 도달");
            return;
        }

        switch (type)
        {
            case UpgradeStatType.Hp: saveData.hpUpgradeLevel++; break;
            case UpgradeStatType.Attack: saveData.attackUpgradeLevel++; break;
            case UpgradeStatType.AttackSpeed: saveData.attackSpeedUpgradeLevel++; break;
        }

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

        return Level >= entry.data.requiredLevel;
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
