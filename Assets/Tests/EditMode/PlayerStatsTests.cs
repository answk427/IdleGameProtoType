using NUnit.Framework;
using Newtonsoft.Json;
using UnityEngine;

[TestFixture]
public class PlayerStatsTests
{
    private PlayerStatDatabase statDatabase;
    private PlayerUpgradeConfig upgradeConfig;
    private PlayerStats stats;

    private static PlayerStatData MakeLevel(int level, int requiredExp, int baseMaxHp, int baseAttackDamage)
    {
        string json = "{" +
            $"\"level\":{level}," +
            $"\"requiredExp\":{requiredExp}," +
            $"\"baseMaxHp\":{baseMaxHp}," +
            $"\"baseAttackDamage\":{baseAttackDamage}," +
            "\"baseAttackInterval\":1.0," +
            "\"baseRunSpeed\":2.0" +
            "}";
        return JsonConvert.DeserializeObject<PlayerStatData>(json);
    }

    [SetUp]
    public void SetUp()
    {
        statDatabase = ScriptableObject.CreateInstance<PlayerStatDatabase>();
        statDatabase.entries.Add(MakeLevel(1, requiredExp: 100, baseMaxHp: 50, baseAttackDamage: 10));
        statDatabase.entries.Add(MakeLevel(2, requiredExp: 200, baseMaxHp: 70, baseAttackDamage: 14));
        statDatabase.entries.Add(MakeLevel(3, requiredExp: 300, baseMaxHp: 90, baseAttackDamage: 18));

        upgradeConfig = ScriptableObject.CreateInstance<PlayerUpgradeConfig>();
        upgradeConfig.settings.hpPerUpgrade = 20;
        upgradeConfig.settings.attackPerUpgrade = 3;
        upgradeConfig.settings.hpBaseCost = 50;
        upgradeConfig.settings.hpCostMultiplier = 1.15f;
        upgradeConfig.settings.attackBaseCost = 80;
        upgradeConfig.settings.attackCostMultiplier = 1.18f;
        upgradeConfig.maxUpgradeLevel = 0;

        stats = new PlayerStats();
        stats.Initialize(statDatabase, upgradeConfig);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(statDatabase);
        Object.DestroyImmediate(upgradeConfig);
    }

    [Test]
    public void FreshPlayer_StartsAtLevel1_WithBaseStats()
    {
        Assert.AreEqual(1, stats.Level);
        Assert.AreEqual(50, stats.MaxHp);
        Assert.AreEqual(10, stats.AttackDamage);
    }

    [Test]
    public void AddExp_BelowRequiredExp_DoesNotLevelUp()
    {
        stats.AddExp(99);
        Assert.AreEqual(1, stats.Level);
        Assert.AreEqual(99, stats.CurrentExp);
    }

    [Test]
    public void AddExp_ExactlyRequiredExp_LevelsUpAndCarriesRemainderZero()
    {
        stats.AddExp(100);
        Assert.AreEqual(2, stats.Level);
        Assert.AreEqual(0, stats.CurrentExp);
        Assert.AreEqual(70, stats.MaxHp);
    }

    [Test]
    public void AddExp_LargeAmount_ChainLevelsUpAndKeepsRemainder()
    {
        // 100(lv1->2) + 200(lv2->3) = 300, 그리고 50 남음. lv3은 마지막 엔트리라 더는 오르지 않음.
        stats.AddExp(350);

        Assert.AreEqual(3, stats.Level);
        Assert.AreEqual(50, stats.CurrentExp);
        Assert.AreEqual(18, stats.AttackDamage);
    }

    [Test]
    public void Upgrade_IncreasesAttackDamageByConfiguredAmount()
    {
        int before = stats.AttackDamage;

        stats.Upgrade(UpgradeStatType.Attack);

        Assert.AreEqual(before + upgradeConfig.settings.attackPerUpgrade, stats.AttackDamage);
        Assert.AreEqual(1, stats.GetCurrentUpgradeLevel(UpgradeStatType.Attack));
    }

    [Test]
    public void GetNextUpgradeCost_MatchesConfigFormula_AndRisesAfterPurchase()
    {
        int firstCost = stats.GetNextUpgradeCost(UpgradeStatType.Hp);
        Assert.AreEqual(upgradeConfig.GetHpUpgradeCost(0), firstCost);

        stats.Upgrade(UpgradeStatType.Hp);

        int secondCost = stats.GetNextUpgradeCost(UpgradeStatType.Hp);
        Assert.AreEqual(upgradeConfig.GetHpUpgradeCost(1), secondCost);
        Assert.Greater(secondCost, firstCost);
    }

    [Test]
    public void IsUpgradeMaxed_BlocksFurtherUpgrades_WhenCapReached()
    {
        upgradeConfig.maxUpgradeLevel = 2;

        stats.Upgrade(UpgradeStatType.Speed);
        stats.Upgrade(UpgradeStatType.Speed);

        Assert.IsTrue(stats.IsUpgradeMaxed(UpgradeStatType.Speed));

        float before = stats.RunSpeed;
        stats.Upgrade(UpgradeStatType.Speed); // 캡 도달 후 호출 - 무시되어야 함
        Assert.AreEqual(before, stats.RunSpeed);
    }
}
