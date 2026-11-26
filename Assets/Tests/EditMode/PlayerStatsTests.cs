using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json;
using UnityEngine;

// 진행도 모델(PlayerStats)의 규칙(docs/COMBAT_ARCHITECTURE.md 10-10).
// - 상태·파생 상태가 명령마다 맞게 바뀌고 한 번만 알린다. 저장 계기 사건은 명령마다 정확히 한 번.
// - 레벨업은 수동이다(D34): 경험치는 쌓이기만 하고, TryLevelUp이 여러 레벨을 한 번에 올리고 나머지를 남기며 최대 레벨에서 멈춘다.
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
            "\"baseAttackInterval\":1.0" +
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
        upgradeConfig.settings.attackSpeedPerUpgrade = 0.05f;
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

    // 구독 직후의 현재 값은 빼고, 그 뒤에 온 값만 모은다.
    private static List<T> Record<T>(IReadOnlyReactiveProperty<T> property)
    {
        var seen = new List<T>();
        bool initial = true;
        property.Subscribe(v => { if (!initial) seen.Add(v); });
        initial = false;
        return seen;
    }

    private static List<T> Record<T>(Observable<T> events)
    {
        var seen = new List<T>();
        events.Subscribe(seen.Add);
        return seen;
    }

    // ── 시작 ──

    [Test]
    public void FreshPlayer_StartsAtLevel1_WithBaseStats()
    {
        Assert.AreEqual(1, stats.Level.Value);
        Assert.AreEqual(0, stats.Exp.Value);
        Assert.AreEqual(100, stats.RequiredExp.Value);
        Assert.IsFalse(stats.CanLevelUp.Value);
        Assert.IsFalse(stats.IsMaxLevel.Value);
        Assert.AreEqual((GameNumber)50, stats.CombatStats.Value.MaxHp);
        Assert.AreEqual((GameNumber)10, stats.CombatStats.Value.AttackDamage);
        Assert.AreEqual(1f, stats.CombatStats.Value.AttackInterval);
    }

    // ── 경험치와 레벨업(D34) ──

    [Test]
    public void AddExp_BelowRequiredExp_OnlyAccumulates()
    {
        stats.AddExp(99);

        Assert.AreEqual(1, stats.Level.Value);
        Assert.AreEqual(99, stats.Exp.Value);
        Assert.IsFalse(stats.CanLevelUp.Value);
    }

    [Test]
    public void AddExp_ExactlyRequiredExp_EnablesLevelUp_AndTryLevelUpCarriesZero()
    {
        stats.AddExp(100);
        Assert.AreEqual(1, stats.Level.Value, "레벨업은 자동이 아니다");
        Assert.IsTrue(stats.CanLevelUp.Value);

        Assert.IsTrue(stats.TryLevelUp());

        Assert.AreEqual(2, stats.Level.Value);
        Assert.AreEqual(0, stats.Exp.Value);
        Assert.AreEqual(200, stats.RequiredExp.Value);
        Assert.IsFalse(stats.CanLevelUp.Value);
        Assert.AreEqual((GameNumber)70, stats.CombatStats.Value.MaxHp);
    }

    [Test]
    public void TryLevelUp_WithLargeExp_ChainsLevels_KeepsRemainder_AndStopsAtMaxLevel()
    {
        // 100(1→2) + 200(2→3) = 300, 50 남음. 3이 최대 레벨이라 더 오르지 않는다.
        stats.AddExp(350);
        Assert.AreEqual(1, stats.Level.Value);

        Assert.IsTrue(stats.TryLevelUp());

        Assert.AreEqual(3, stats.Level.Value);
        Assert.AreEqual(50, stats.Exp.Value);
        Assert.IsTrue(stats.IsMaxLevel.Value);
        Assert.IsFalse(stats.CanLevelUp.Value);
        Assert.AreEqual((GameNumber)18, stats.CombatStats.Value.AttackDamage);
    }

    [Test]
    public void TryLevelUp_NotifiesOnce_WithFinalValues()
    {
        List<int> levels = Record(stats.Level);
        List<int> exps = Record(stats.Exp);
        List<PlayerCombatStats> combat = Record(stats.CombatStats);
        List<int> reached = Record(stats.LevelReached);
        stats.AddExp(350);
        exps.Clear();

        stats.TryLevelUp();

        CollectionAssert.AreEqual(new[] { 3 }, levels, "여러 레벨을 올려도 레벨 알림은 끝 레벨로 한 번");
        CollectionAssert.AreEqual(new[] { 50 }, exps);
        Assert.AreEqual(1, combat.Count, "전투 능력치도 최종 값으로 한 번");
        CollectionAssert.AreEqual(new[] { 3 }, reached, "저장 계기도 한 번");
    }

    [Test]
    public void TryLevelUp_WhenNotAllowed_ReturnsFalse_AndRaisesNothing()
    {
        List<int> reached = Record(stats.LevelReached);
        stats.AddExp(99);

        Assert.IsFalse(stats.TryLevelUp());
        Assert.IsEmpty(reached);
        Assert.AreEqual(1, stats.Level.Value);
    }

    [Test]
    public void AddExp_AtMaxLevel_OrNonPositive_IsIgnored()
    {
        stats.AddExp(-5);
        stats.AddExp(0);
        Assert.AreEqual(0, stats.Exp.Value);

        stats.AddExp(300);
        stats.TryLevelUp();
        Assert.IsTrue(stats.IsMaxLevel.Value);
        int atMax = stats.Exp.Value;

        stats.AddExp(1000);
        Assert.AreEqual(atMax, stats.Exp.Value);
    }

    // 극단값: 필요 경험치가 0인 데이터도 TryLevelUp이 끝난다(최대 레벨에서 멈춤).
    [Test]
    public void ZeroRequiredExp_LevelUpStillTerminatesAtMaxLevel()
    {
        var zero = ScriptableObject.CreateInstance<PlayerStatDatabase>();
        try
        {
            zero.entries.Add(MakeLevel(1, requiredExp: 0, baseMaxHp: 50, baseAttackDamage: 10));
            zero.entries.Add(MakeLevel(2, requiredExp: 0, baseMaxHp: 70, baseAttackDamage: 14));
            zero.entries.Add(MakeLevel(3, requiredExp: 0, baseMaxHp: 90, baseAttackDamage: 18));
            var s = new PlayerStats();
            s.Initialize(zero, upgradeConfig);

            Assert.IsTrue(s.CanLevelUp.Value);
            Assert.IsTrue(s.TryLevelUp());
            Assert.AreEqual(3, s.Level.Value);
            Assert.IsFalse(s.CanLevelUp.Value);
        }
        finally
        {
            Object.DestroyImmediate(zero);
        }
    }

    // ── 상태의 모양 ──

    [Test]
    public void State_GivesCurrentValueOnSubscribe_AndIgnoresSameValue()
    {
        var all = new List<int>();
        stats.Level.Subscribe(all.Add);
        CollectionAssert.AreEqual(new[] { 1 }, all, "구독하는 순간 지금 값");

        stats.LoadSave(new PlayerStatsSaveData()); // 같은 레벨(1)
        CollectionAssert.AreEqual(new[] { 1 }, all, "같은 값은 알리지 않는다");
    }

    // ── 업그레이드 ──

    [Test]
    public void Upgrade_IncreasesAttackDamage_UpdatesLevelState_AndRaisesUpgradedOnce()
    {
        GameNumber before = stats.CombatStats.Value.AttackDamage;
        List<UpgradeStatType> upgraded = Record(stats.Upgraded);

        stats.Upgrade(UpgradeStatType.Attack);

        Assert.AreEqual(before + upgradeConfig.settings.attackPerUpgrade, stats.CombatStats.Value.AttackDamage);
        Assert.AreEqual(1, stats.UpgradeLevel(UpgradeStatType.Attack).Value);
        Assert.AreEqual(0, stats.UpgradeLevel(UpgradeStatType.Hp).Value);
        CollectionAssert.AreEqual(new[] { UpgradeStatType.Attack }, upgraded);
    }

    [Test]
    public void GetNextUpgradeCost_MatchesConfigFormula_AndRisesAfterPurchase()
    {
        GameNumber firstCost = stats.GetNextUpgradeCost(UpgradeStatType.Hp);
        Assert.AreEqual(upgradeConfig.GetHpUpgradeCost(0), firstCost);

        stats.Upgrade(UpgradeStatType.Hp);

        GameNumber secondCost = stats.GetNextUpgradeCost(UpgradeStatType.Hp);
        Assert.AreEqual(upgradeConfig.GetHpUpgradeCost(1), secondCost);
        Assert.Greater(secondCost, firstCost);
    }

    [Test]
    public void IsUpgradeMaxed_BlocksFurtherUpgrades_WhenCapReached()
    {
        upgradeConfig.maxUpgradeLevel = 2;

        stats.Upgrade(UpgradeStatType.AttackSpeed);
        stats.Upgrade(UpgradeStatType.AttackSpeed);
        Assert.IsTrue(stats.IsUpgradeMaxed(UpgradeStatType.AttackSpeed));

        List<UpgradeStatType> upgraded = Record(stats.Upgraded);
        float before = stats.CombatStats.Value.AttackInterval;
        stats.Upgrade(UpgradeStatType.AttackSpeed); // 캡 도달 후 호출 - 무시되어야 함

        Assert.AreEqual(before, stats.CombatStats.Value.AttackInterval);
        Assert.AreEqual(2, stats.UpgradeLevel(UpgradeStatType.AttackSpeed).Value);
        Assert.IsEmpty(upgraded, "무시된 명령은 저장 계기를 내지 않는다");
    }

    // ── 스킬 장착 ──

    [Test]
    public void EquipSkill_RaisesEquipChangedOnce_UnchangedRaisesNothing()
    {
        stats.LoadSave(new PlayerStatsSaveData { learnedSkillIds = new List<int> { 1, 2 } });
        List<int> changed = Record(stats.SkillEquipChanged);

        Assert.AreEqual(SkillSlotResult.Success, stats.EquipSkill(1, 0));
        Assert.AreEqual(SkillSlotResult.Unchanged, stats.EquipSkill(1, 0));
        Assert.AreEqual(SkillSlotResult.NotLearned, stats.EquipSkill(9, 1));
        Assert.AreEqual(SkillSlotResult.Success, stats.UnequipSkill(0));

        CollectionAssert.AreEqual(new[] { 0, 0 }, changed);
        Assert.AreEqual(PlayerStatsSaveData.EmptySlot, stats.EquippedSlots.Value[0]);
    }
}
