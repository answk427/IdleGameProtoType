public class HpUpgradeStat : IUpgradeStat
{
    private readonly PlayerStats stats;
    private readonly PlayerUpgradeConfig config;

    public HpUpgradeStat(PlayerStats stats, PlayerUpgradeConfig config)
    {
        this.stats = stats;
        this.config = config;
    }

    public UpgradeStatType Type => UpgradeStatType.Hp;

    public int GetLevel() => stats.GetSaveData().hpUpgradeLevel;
    public void IncrementLevel() => stats.GetSaveData().hpUpgradeLevel++;
    public int GetCost(int level) => config.GetHpUpgradeCost(level);
    public float GetCurrentValue() => stats.MaxHp;
    public float GetNextValue() => stats.MaxHp + config.settings.hpPerUpgrade;
}
