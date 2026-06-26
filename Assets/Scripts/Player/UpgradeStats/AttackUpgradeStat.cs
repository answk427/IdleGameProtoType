public class AttackUpgradeStat : IUpgradeStat
{
    private readonly PlayerStats stats;
    private readonly PlayerUpgradeConfig config;

    public AttackUpgradeStat(PlayerStats stats, PlayerUpgradeConfig config)
    {
        this.stats = stats;
        this.config = config;
    }

    public UpgradeStatType Type => UpgradeStatType.Attack;

    public int GetLevel() => stats.GetSaveData().attackUpgradeLevel;
    public void IncrementLevel() => stats.GetSaveData().attackUpgradeLevel++;
    public int GetCost(int level) => config.GetAttackUpgradeCost(level);
    public float GetCurrentValue() => stats.AttackDamage;
    public float GetNextValue() => stats.AttackDamage + config.settings.attackPerUpgrade;
}
