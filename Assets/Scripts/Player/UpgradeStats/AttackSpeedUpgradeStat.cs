using UnityEngine;

public class AttackSpeedUpgradeStat : IUpgradeStat
{
    private readonly PlayerStats stats;
    private readonly PlayerUpgradeConfig config;

    public AttackSpeedUpgradeStat(PlayerStats stats, PlayerUpgradeConfig config)
    {
        this.stats = stats;
        this.config = config;
    }

    public int GetLevel() => stats.GetSaveData().attackSpeedUpgradeLevel;
    public void IncrementLevel() => stats.GetSaveData().attackSpeedUpgradeLevel++;
    public int GetCost(int level) => config.GetAttackSpeedUpgradeCost(level);

    // 공격 간격(초)은 작을수록 강해지는 값이라 그대로 보여주면 직관과 반대로 읽힌다.
    // 다른 스탯들과 동일하게 "클수록 좋다"가 되도록 초당 공격 횟수로 환산해서 보여준다.
    public float GetCurrentValue() => 1f / stats.AttackInterval;

    public float GetNextValue()
    {
        float nextInterval = Mathf.Max(0.1f, stats.AttackInterval - config.settings.attackSpeedPerUpgrade);
        return 1f / nextInterval;
    }
}
