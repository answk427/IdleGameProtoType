using System;
using UnityEngine;

/// <summary>
/// 플레이어 최종 스탯 계산기
/// 
/// 최종 스탯 = 레벨 기본 스탯 (PlayerStatData) + 업그레이드 보너스 (PlayerSaveData)
/// 
/// 사용법:
///   stats.Initialize(statData, upgradeConfig);
///   stats.LoadSave(saveData);
///   int hp = stats.MaxHp; // 최종 HP
///   stats.AddExp(30);     // 경험치 추가 → 레벨업 자동 처리
///   stats.UpgradeHp();    // 돈 소비 후 호출
/// </summary>
[System.Serializable]
public class PlayerStats
{
    // ── 이벤트 ──────────────────────────────────────────────
    public event Action<int> OnLevelUp;           // 레벨업 시 (새 레벨 전달)
    public event Action<int, int> OnExpChanged;   // 경험치 변화 시 (현재, 필요)
    public event Action OnUpgraded;               // 업그레이드 구매 시

    // ── 참조 ────────────────────────────────────────────────
    private PlayerStatData statData;
    private PlayerUpgradeConfig upgradeConfig;
    private PlayerSaveData saveData;

    // ── 읽기 전용 프로퍼티 ──────────────────────────────────

    public int Level => saveData?.level ?? 1;
    public int CurrentExp => saveData?.currentExp ?? 0;
    public int RequiredExp => statData?.GetRequiredExp(Level) ?? 999;
    public bool IsMaxLevel => Level >= (statData?.MaxLevel ?? 1);

    // 최종 스탯 = 기본 + 업그레이드 보너스
    public int MaxHp => BaseStat.baseMaxHp + (saveData?.hpUpgradeLevel ?? 0) * upgradeConfig.settings.hpPerUpgrade;
    public int AttackDamage => BaseStat.baseAttackDamage + (saveData?.attackUpgradeLevel ?? 0) * upgradeConfig.settings.attackPerUpgrade;
    public float AttackInterval => Mathf.Max(0.1f, BaseStat.baseAttackInterval);
    public float RunSpeed => BaseStat.baseRunSpeed + (saveData?.speedUpgradeLevel ?? 0) * upgradeConfig.settings.speedPerUpgrade;

    // 업그레이드 다음 비용
    public int NextHpUpgradeCost => upgradeConfig.GetHpUpgradeCost(saveData?.hpUpgradeLevel ?? 0);
    public int NextAttackUpgradeCost => upgradeConfig.GetAttackUpgradeCost(saveData?.attackUpgradeLevel ?? 0);
    public int NextSpeedUpgradeCost => upgradeConfig.GetSpeedUpgradeCost(saveData?.speedUpgradeLevel ?? 0);

    private PlayerStatData.LevelStat BaseStat => statData.GetStat(Level);

    // ── 초기화 ──────────────────────────────────────────────

    public void Initialize(PlayerStatData statData, PlayerUpgradeConfig upgradeConfig)
    {
        this.statData = statData;
        this.upgradeConfig = upgradeConfig;
        this.saveData = new PlayerSaveData(); // 기본값
    }

    public void LoadSave(PlayerSaveData data)
    {
        saveData = data;
    }

    // ── 경험치 / 레벨업 ─────────────────────────────────────

    /// <summary>경험치 추가. 레벨업 조건이면 자동으로 레벨업 처리.</summary>
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

    // ── 업그레이드 (돈 소비는 외부에서 검사 후 호출) ────────

    /// <summary>
    /// HP 업그레이드. 돈 차감은 호출자(UI 등)에서 먼저 처리 후 호출.
    /// 업그레이드 가능 여부는 CanUpgrade()로 미리 확인.
    /// </summary>
    public void UpgradeHp()
    {
        if (!CanUpgrade(saveData.hpUpgradeLevel)) return;
        saveData.hpUpgradeLevel++;
        OnUpgraded?.Invoke();
        Debug.Log($"[PlayerStats] HP 업그레이드 Lv.{saveData.hpUpgradeLevel} → MaxHp:{MaxHp}");
    }

    public void UpgradeAttack()
    {
        if (!CanUpgrade(saveData.attackUpgradeLevel)) return;
        saveData.attackUpgradeLevel++;
        OnUpgraded?.Invoke();
        Debug.Log($"[PlayerStats] 공격력 업그레이드 Lv.{saveData.attackUpgradeLevel} → ATK:{AttackDamage}");
    }

    public void UpgradeSpeed()
    {
        if (!CanUpgrade(saveData.speedUpgradeLevel)) return;
        saveData.speedUpgradeLevel++;
        OnUpgraded?.Invoke();
        Debug.Log($"[PlayerStats] 속도 업그레이드 Lv.{saveData.speedUpgradeLevel} → Speed:{RunSpeed}");
    }

    private bool CanUpgrade(int currentUpgradeLevel)
    {
        if (upgradeConfig.maxUpgradeLevel > 0 && currentUpgradeLevel >= upgradeConfig.maxUpgradeLevel)
        {
            Debug.LogWarning("[PlayerStats] 최대 업그레이드 도달");
            return false;
        }
        return true;
    }

    // ── 저장 ────────────────────────────────────────────────

    public void Save() => saveData?.Save();
    public PlayerSaveData GetSaveData() => saveData;
}
