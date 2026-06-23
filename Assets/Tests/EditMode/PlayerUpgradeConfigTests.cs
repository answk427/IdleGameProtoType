using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class PlayerUpgradeConfigTests
{
    private PlayerUpgradeConfig config;

    [SetUp]
    public void SetUp()
    {
        config = ScriptableObject.CreateInstance<PlayerUpgradeConfig>();
        config.settings.hpBaseCost = 50;
        config.settings.hpCostMultiplier = 1.15f;
        config.settings.attackBaseCost = 80;
        config.settings.attackCostMultiplier = 1.18f;
        config.settings.speedBaseCost = 100;
        config.settings.speedCostMultiplier = 1.20f;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(config);
    }

    [Test]
    public void HpUpgradeCost_AtLevelZero_EqualsBaseCost()
    {
        Assert.AreEqual(50, config.GetHpUpgradeCost(0));
    }

    [Test]
    public void HpUpgradeCost_GrowsByMultiplierPerLevel()
    {
        int expectedLevel3 = Mathf.RoundToInt(50 * Mathf.Pow(1.15f, 3));
        Assert.AreEqual(expectedLevel3, config.GetHpUpgradeCost(3));
    }

    [Test]
    public void AttackUpgradeCost_AtLevelZero_EqualsBaseCost()
    {
        Assert.AreEqual(80, config.GetAttackUpgradeCost(0));
    }

    [Test]
    public void SpeedUpgradeCost_AtLevelZero_EqualsBaseCost()
    {
        Assert.AreEqual(100, config.GetSpeedUpgradeCost(0));
    }

    [Test]
    public void UpgradeCost_NeverDecreasesAsLevelIncreases()
    {
        int prev = config.GetHpUpgradeCost(0);
        for (int level = 1; level <= 20; level++)
        {
            int cost = config.GetHpUpgradeCost(level);
            Assert.GreaterOrEqual(cost, prev, $"cost at level {level} should not be lower than previous level");
            prev = cost;
        }
    }
}
