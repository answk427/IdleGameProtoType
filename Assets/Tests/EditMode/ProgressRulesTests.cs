using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// 진행도의 규칙 중 UI가 구독하는 파생 상태(docs/UI_DESIGN.md 5-8, 10-6). 진짜 GameSession.
// - 해금: 저장하지 않는 파생 상태. 조건의 경계에서 열리고, 진행도가 내려가면 다시 잠긴다. 표에 행이 없으면 잠기고 에러.
// - 레드닷 조건: 업그레이드를 살 수 있음(TryUpgrade와 같은 판정), 배울 스킬이 있음(학습 조건과 같은 규칙).
// - 데이터 검사: 모든 콘텐츠에 행이 있고, 조건이 닿을 수 있는 값이다.
[TestFixture]
public class ProgressRulesTests
{
    private PlayerStatDatabase statDatabase;
    private PlayerUpgradeConfig upgradeConfig;
    private readonly List<Object> assets = new List<Object>();

    private static PlayerStatData MakeLevel(int level, int requiredExp) =>
        JsonConvert.DeserializeObject<PlayerStatData>(
            $"{{\"level\":{level},\"requiredExp\":{requiredExp},\"baseMaxHp\":100,\"baseAttackDamage\":10,\"baseAttackInterval\":1.0}}");

    private static ContentUnlockData Rule(GameContent content, UnlockConditionType type, int value) =>
        JsonConvert.DeserializeObject<ContentUnlockData>(
            $"{{\"content\":\"{content}\",\"conditionType\":\"{type}\",\"conditionValue\":{value}}}");

    private ContentUnlockDatabase Table(params ContentUnlockData[] rows)
    {
        var table = ScriptableObject.CreateInstance<ContentUnlockDatabase>();
        table.entries.AddRange(rows);
        assets.Add(table);
        return table;
    }

    // 지금 게임과 같은 표: 캐릭터는 처음부터, 스킬은 레벨 2부터.
    private ContentUnlockDatabase GameTable() => Table(
        Rule(GameContent.Character, UnlockConditionType.None, 0),
        Rule(GameContent.Skill, UnlockConditionType.PlayerLevel, 2));

    [SetUp]
    public void SetUp()
    {
        statDatabase = ScriptableObject.CreateInstance<PlayerStatDatabase>();
        for (int level = 1; level <= 5; level++) statDatabase.entries.Add(MakeLevel(level, 100));

        upgradeConfig = ScriptableObject.CreateInstance<PlayerUpgradeConfig>();
        upgradeConfig.settings.hpPerUpgrade = 20;
        upgradeConfig.settings.attackPerUpgrade = 3;
        upgradeConfig.settings.attackSpeedPerUpgrade = 0.05f;
        upgradeConfig.settings.hpBaseCost = 50;
        upgradeConfig.settings.hpCostMultiplier = 2f;
        upgradeConfig.settings.attackBaseCost = 80;
        upgradeConfig.settings.attackCostMultiplier = 2f;
        upgradeConfig.settings.attackSpeedBaseCost = 120;
        upgradeConfig.settings.attackSpeedCostMultiplier = 2f;
        upgradeConfig.maxUpgradeLevel = 0;

        assets.Add(statDatabase);
        assets.Add(upgradeConfig);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in assets) Object.DestroyImmediate(asset);
        assets.Clear();
    }

    private GameSession Session(ContentUnlockDatabase unlocks = null, IReadOnlyList<SkillData> skills = null, int level = 1, int stage = 1, double gold = 0)
    {
        var session = new GameSession(statDatabase, upgradeConfig, skills, unlocks);
        session.Apply(new PlayerSaveData { stats = new PlayerStatsSaveData { level = level }, currentStageNumber = stage, gold = gold });
        return session;
    }

    // ── 해금 ──

    [Test]
    public void Unlock_OpensExactlyAtTheLevel_AndRelocksWhenProgressGoesDown()
    {
        GameSession session = Session(GameTable());
        IReadOnlyReactiveProperty<bool> skill = session.Unlocks.IsUnlocked(GameContent.Skill);
        var seen = new List<bool>();
        skill.Subscribe(seen.Add);

        Assert.IsTrue(session.Unlocks.IsUnlocked(GameContent.Character).Value, "조건 없음 = 처음부터");
        Assert.IsFalse(skill.Value, "레벨 1");

        session.Stats.AddExp(100);
        session.Stats.TryLevelUp();
        Assert.IsTrue(skill.Value, "레벨 2가 된 순간");

        session.Apply(new PlayerSaveData { stats = new PlayerStatsSaveData { level = 1 } });
        Assert.IsFalse(skill.Value, "진행도를 되돌리면 다시 잠긴다(저장하지 않는 파생 상태)");
        CollectionAssert.AreEqual(new[] { false, true, false }, seen);
        Assert.AreSame(skill, session.Unlocks.IsUnlocked(GameContent.Skill), "같은 콘텐츠는 같은 상태 객체");
    }

    [Test]
    public void Unlock_ByStageReached_FollowsSavedStage()
    {
        GameSession session = Session(Table(
            Rule(GameContent.Character, UnlockConditionType.None, 0),
            Rule(GameContent.Skill, UnlockConditionType.StageReached, 3)), stage: 2);
        IReadOnlyReactiveProperty<bool> skill = session.Unlocks.IsUnlocked(GameContent.Skill);

        Assert.IsFalse(skill.Value);
        session.SetCurrentStageNumber(3);
        Assert.IsTrue(skill.Value, "스테이지 3에 들어간 순간(입장 = 저장된 스테이지)");
        session.SetCurrentStageNumber(int.MaxValue);
        Assert.IsTrue(skill.Value);
        Assert.AreEqual(new UnlockCondition(UnlockConditionType.StageReached, 3), session.Unlocks.ConditionOf(GameContent.Skill));
    }

    // 행이 빠진 콘텐츠는 조용히 열리지 않는다. 에러는 한 번만.
    [Test]
    public void MissingRow_IsLocked_AndLogsOnce()
    {
        GameSession session = Session(Table(Rule(GameContent.Character, UnlockConditionType.None, 0)), level: 5);

        LogAssert.Expect(LogType.Error, new Regex("Skill의 행이 없어"));
        Assert.IsFalse(session.Unlocks.IsUnlocked(GameContent.Skill).Value);
        session.Stats.AddExp(1); // 다시 계산할 일이 생겨도 에러를 또 내지 않는다
        session.Apply(new PlayerSaveData { stats = new PlayerStatsSaveData { level = 4 } });
        Assert.AreEqual(UnlockCondition.None, session.Unlocks.ConditionOf(GameContent.Skill));
    }

    [Test]
    public void NoTable_LocksEverythingAsked()
    {
        GameSession session = Session(null, level: 5);

        LogAssert.Expect(LogType.Error, new Regex("Character의 행이 없어"));
        Assert.IsFalse(session.Unlocks.IsUnlocked(GameContent.Character).Value);
    }

    [Test]
    public void Validate_FindsMissingRowsAndUnreachableValues()
    {
        Func<int, bool> stages = stage => stage >= 1 && stage <= 10;

        CollectionAssert.IsEmpty(ContentUnlockDatabase.Validate(GameTable(), maxLevel: 5, stages));

        List<string> missing = ContentUnlockDatabase.Validate(Table(Rule(GameContent.Skill, UnlockConditionType.None, 0)), 5, stages);
        Assert.AreEqual(1, missing.Count);
        StringAssert.Contains("Character의 행이 없습니다", missing[0]);

        List<string> unreachable = ContentUnlockDatabase.Validate(Table(
            Rule(GameContent.Character, UnlockConditionType.PlayerLevel, 6),
            Rule(GameContent.Skill, UnlockConditionType.StageReached, 11)), 5, stages);
        Assert.AreEqual(2, unreachable.Count, string.Join("\n", unreachable));

        List<string> zero = ContentUnlockDatabase.Validate(Table(
            Rule(GameContent.Character, UnlockConditionType.PlayerLevel, 0),
            Rule(GameContent.Skill, UnlockConditionType.None, 0)), 5, stages);
        Assert.AreEqual(1, zero.Count, "레벨 0은 표 밖");

        Assert.AreEqual(1, ContentUnlockDatabase.Validate(null, 5, stages).Count);
    }

    // ── 레드닷: 업그레이드를 살 수 있음 ──

    [Test]
    public void CanBuyUpgrade_IsTheSameRuleAsTryUpgrade()
    {
        GameSession session = Session(gold: 49);
        var seen = new List<bool>();
        session.CanBuyUpgrade.Subscribe(seen.Add);

        Assert.IsFalse(session.CanBuyUpgrade.Value, "가장 싼 업그레이드(HP 50)에 1 모자람");
        Assert.AreEqual(UpgradeResult.NotEnoughGold, session.CheckUpgrade(UpgradeStatType.Hp));

        session.Wallet.AddGold((GameNumber)1);
        Assert.IsTrue(session.CanBuyUpgrade.Value, "골드 = 비용인 경계");
        Assert.AreEqual(UpgradeResult.Success, session.TryUpgrade(UpgradeStatType.Hp));
        Assert.IsFalse(session.CanBuyUpgrade.Value, "사고 나면 골드 0");

        CollectionAssert.AreEqual(new[] { false, true, false }, seen, "값이 바뀔 때만 알린다");
    }

    [Test]
    public void CanBuyUpgrade_FalseWhenEverythingIsMaxed_EvenWithHugeGold()
    {
        upgradeConfig.maxUpgradeLevel = 1;
        GameSession session = Session(gold: 1e300);
        foreach (UpgradeStatType type in Enum.GetValues(typeof(UpgradeStatType))) session.TryUpgrade(type);

        Assert.IsFalse(session.CanBuyUpgrade.Value);
        Assert.AreEqual(UpgradeResult.MaxLevel, session.CheckUpgrade(UpgradeStatType.Attack));
    }

    [Test]
    public void CanBuyUpgrade_FollowsProgressReplacement()
    {
        GameSession session = Session(gold: 0);
        session.Apply(new PlayerSaveData { stats = new PlayerStatsSaveData { level = 1 }, gold = 1000 });
        Assert.IsTrue(session.CanBuyUpgrade.Value);
    }

    // 골드는 처치마다 바뀐다. 레드닷 판정이 그 길에 할당을 더하지 않는다.
    [Test]
    public void GoldChanges_DoNotAllocate_ThroughRedDotCondition()
    {
        GameSession session = Session(gold: 0);
        bool last = false;
        using (session.CanBuyUpgrade.Subscribe(v => last = v))
        {
            for (int i = 0; i < 100; i++) session.Wallet.AddGold((GameNumber)1); // 준비

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) session.Wallet.AddGold((GameNumber)1);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated);
            Assert.IsTrue(last);
        }
    }

    // ── 레드닷: 배울 스킬이 있음 ──

    [Test]
    public void HasLearnableSkill_FollowsLevelAndLearned()
    {
        var skills = new[] { TestSkills.CreateData(id: 1, requiredLevel: 1), TestSkills.CreateData(id: 2, requiredLevel: 3) };
        GameSession session = Session(skills: skills);
        PlayerStats stats = session.Stats;

        Assert.IsTrue(stats.HasLearnableSkill.Value, "레벨 1 스킬을 아직 안 배움");
        Assert.AreEqual(LearnSkillResult.Success, stats.LearnSkill(1));
        Assert.IsFalse(stats.HasLearnableSkill.Value, "남은 스킬은 레벨 3부터");
        Assert.AreEqual(LearnSkillResult.LevelTooLow, stats.LearnSkill(2));

        session.Apply(new PlayerSaveData { stats = new PlayerStatsSaveData { level = 3, learnedSkillIds = new List<int> { 1 } } });
        Assert.IsTrue(stats.HasLearnableSkill.Value);
        Assert.AreEqual(LearnSkillResult.Success, stats.LearnSkill(2));
        Assert.IsFalse(stats.HasLearnableSkill.Value, "전부 배움");
        Assert.AreEqual(LearnSkillResult.UnknownSkill, stats.LearnSkill(99), "표에 없는 스킬");
    }

    [Test]
    public void NoSkills_NothingToLearn()
    {
        GameSession session = Session(level: 5);
        Assert.IsFalse(session.Stats.HasLearnableSkill.Value);
        Assert.AreEqual(LearnSkillResult.UnknownSkill, session.Stats.LearnSkill(1));
    }
}
