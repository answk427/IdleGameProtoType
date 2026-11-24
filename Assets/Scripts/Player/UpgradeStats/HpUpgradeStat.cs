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
    public GameNumber GetCost(int level) => config.GetHpUpgradeCost(level);
    // 업그레이드 표시 API는 아직 float(UI 이관 때 GameNumber로, D14)
    public float GetCurrentValue() => (float)stats.MaxHp.ToDouble();
    public float GetNextValue() => (float)(stats.MaxHp.ToDouble() + config.settings.hpPerUpgrade);
}
