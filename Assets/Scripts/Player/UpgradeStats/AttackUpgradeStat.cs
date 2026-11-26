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
    public GameNumber GetCost(int level) => config.GetAttackUpgradeCost(level);
    // 업그레이드 표시 API는 아직 float(UI 이관 때 GameNumber로, D14)
    public float GetCurrentValue() => (float)stats.CombatStats.Value.AttackDamage.ToDouble();
    public float GetNextValue() => (float)(stats.CombatStats.Value.AttackDamage.ToDouble() + config.settings.attackPerUpgrade);
}
