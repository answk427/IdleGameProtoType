public class AttackSpeedUpgradeStat : IUpgradeStat
{
    private readonly PlayerStats stats;
    private readonly PlayerUpgradeConfig config;

    public AttackSpeedUpgradeStat(PlayerStats stats, PlayerUpgradeConfig config)
    {
        this.stats = stats;
        this.config = config;
    }

    public UpgradeStatType Type => UpgradeStatType.AttackSpeed;

    public int GetLevel() => stats.GetSaveData().attackSpeedUpgradeLevel;
    public void IncrementLevel() => stats.GetSaveData().attackSpeedUpgradeLevel++;
    public GameNumber GetCost(int level) => config.GetAttackSpeedUpgradeCost(level);
}
