using System;
using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

// 캐릭터 탭 Presenter: 진짜 GameSession(진행도·골드·업그레이드 규칙) + 가짜 View.
// - 표시 문자열(레벨, 경험치, "현재 → 다음", 비용, MAX)을 Presenter가 만들고, 진행도의 상태가 바뀌면 따라온다.
// - "다음" 값은 진행도의 미리보기라 실제로 올린 뒤의 값과 같다. 공격 간격 하한에서도 단계·비용은 따라온다.
// - 진행도를 통째로 바꿔도(Apply) 화면이 맞고, 닫힌 뒤에는 반응하지 않는다.
[TestFixture]
public class CharacterTabPresenterTests
{
    private sealed class FakeView : ICharacterTabView
    {
        public readonly List<UpgradeStatType> Rows = new List<UpgradeStatType>
            { UpgradeStatType.Hp, UpgradeStatType.Attack, UpgradeStatType.AttackSpeed };
        public readonly Dictionary<UpgradeStatType, UpgradeRowModel> Shown = new Dictionary<UpgradeStatType, UpgradeRowModel>();

        public string Level, ExpText, ExpPercent;
        public float ExpFill;
        public bool LevelUpAvailable;
        public int RowCalls;

        public event Action LevelUpTapped;
        public event Action<UpgradeStatType> UpgradeTapped;

        public IReadOnlyList<UpgradeStatType> UpgradeRows => Rows;

        public void SetLevel(string text) => Level = text;
        public void SetExp(float fillRatio, string text, string percentText) { ExpFill = fillRatio; ExpText = text; ExpPercent = percentText; }
        public void SetLevelUpAvailable(bool available) => LevelUpAvailable = available;

        public void SetUpgradeRow(UpgradeStatType type, UpgradeRowModel row)
        {
            RowCalls++;
            Shown[type] = row;
        }

        public void TapLevelUp() => LevelUpTapped?.Invoke();
        public void TapUpgrade(UpgradeStatType type) => UpgradeTapped?.Invoke(type);
        public bool HasListeners => LevelUpTapped != null || UpgradeTapped != null;
    }

    private PlayerStatDatabase statDatabase;
    private PlayerUpgradeConfig upgradeConfig;
    private GameSession session;
    private FakeView view;
    private CharacterTabPresenter presenter;

    private static PlayerStatData MakeLevel(int level, int requiredExp, int baseMaxHp, int baseAttackDamage) =>
        JsonConvert.DeserializeObject<PlayerStatData>(
            $"{{\"level\":{level},\"requiredExp\":{requiredExp},\"baseMaxHp\":{baseMaxHp},\"baseAttackDamage\":{baseAttackDamage},\"baseAttackInterval\":1.0}}");

    [SetUp]
    public void SetUp()
    {
        statDatabase = ScriptableObject.CreateInstance<PlayerStatDatabase>();
        statDatabase.entries.Add(MakeLevel(1, 100, 50, 10));
        statDatabase.entries.Add(MakeLevel(2, 200, 70, 14));
        statDatabase.entries.Add(MakeLevel(3, 300, 90, 18));

        upgradeConfig = ScriptableObject.CreateInstance<PlayerUpgradeConfig>();
        upgradeConfig.settings.hpPerUpgrade = 20;
        upgradeConfig.settings.attackPerUpgrade = 3;
        upgradeConfig.settings.attackSpeedPerUpgrade = 0.25f; // 1.0 → 0.75 → 0.5 → 0.25 → 하한 0.1
        upgradeConfig.settings.hpBaseCost = 50;
        upgradeConfig.settings.hpCostMultiplier = 2f;
        upgradeConfig.settings.attackBaseCost = 80;
        upgradeConfig.settings.attackCostMultiplier = 2f;
        upgradeConfig.settings.attackSpeedBaseCost = 10;
        upgradeConfig.settings.attackSpeedCostMultiplier = 2f;
        upgradeConfig.maxUpgradeLevel = 0;

        session = new GameSession(statDatabase, upgradeConfig);
        view = new FakeView();
    }

    [TearDown]
    public void TearDown()
    {
        presenter?.Dispose();
        presenter = null;
        Object.DestroyImmediate(statDatabase);
        Object.DestroyImmediate(upgradeConfig);
    }

    private void Bind() => presenter = new CharacterTabPresenter(view, session.Stats, session.Wallet.Gold, session.TryUpgrade);

    private UpgradeRowModel Row(UpgradeStatType type) => view.Shown[type];

    [Test]
    public void Bind_DrawsHeaderAndEveryRow()
    {
        Bind();

        Assert.AreEqual("Lv. 1", view.Level);
        Assert.AreEqual("EXP 0 / 100", view.ExpText);
        Assert.AreEqual("0.00%", view.ExpPercent);
        Assert.AreEqual(0f, view.ExpFill);
        Assert.IsFalse(view.LevelUpAvailable);

        Assert.AreEqual("Lv.1", Row(UpgradeStatType.Hp).Level);
        Assert.AreEqual("50 → 70", Row(UpgradeStatType.Hp).Value);
        Assert.AreEqual("50", Row(UpgradeStatType.Hp).Cost);
        Assert.IsFalse(Row(UpgradeStatType.Hp).CanUpgrade, "골드 0");

        Assert.AreEqual("10 → 13", Row(UpgradeStatType.Attack).Value);
        Assert.AreEqual("1.00 → 1.33", Row(UpgradeStatType.AttackSpeed).Value, "공격 간격은 초당 공격 횟수로");
    }

    [Test]
    public void Gold_DecidesWhichRowsCanUpgrade()
    {
        Bind();
        session.Wallet.AddGold((GameNumber)60);

        Assert.IsTrue(Row(UpgradeStatType.Hp).CanUpgrade, "50 ≤ 60");
        Assert.IsFalse(Row(UpgradeStatType.Attack).CanUpgrade, "80 > 60");
        Assert.IsTrue(Row(UpgradeStatType.AttackSpeed).CanUpgrade);
    }

    [Test]
    public void UpgradeTap_SpendsGold_AndRedrawsRow()
    {
        Bind();
        session.Wallet.AddGold((GameNumber)120);

        view.TapUpgrade(UpgradeStatType.Hp);

        Assert.AreEqual((GameNumber)70, session.Wallet.Gold.Value);
        Assert.AreEqual("Lv.2", Row(UpgradeStatType.Hp).Level);
        Assert.AreEqual("70 → 90", Row(UpgradeStatType.Hp).Value);
        Assert.AreEqual("100", Row(UpgradeStatType.Hp).Cost);
        Assert.IsFalse(Row(UpgradeStatType.Hp).CanUpgrade, "남은 70 < 100");
    }

    [Test]
    public void UpgradeTap_WithoutGold_ChangesNothing()
    {
        Bind();
        view.TapUpgrade(UpgradeStatType.Attack);

        Assert.AreEqual(0, session.Stats.UpgradeLevel(UpgradeStatType.Attack).Value);
        Assert.AreEqual("Lv.1", Row(UpgradeStatType.Attack).Level);
    }

    [Test]
    public void NextValue_IsWhatUpgradingActuallyGives()
    {
        Bind();
        session.Wallet.AddGold((GameNumber)1000);

        foreach (UpgradeStatType type in view.Rows)
        {
            string promised = Row(type).Value.Split('→')[1].Trim();
            view.TapUpgrade(type);
            string now = Row(type).Value.Split('→')[0].Trim();
            Assert.AreEqual(promised, now, $"{type}: 화면이 말한 '다음'이 올린 뒤의 '현재'");
        }
    }

    [Test]
    public void AttackIntervalAtFloor_LevelAndCostStillFollow()
    {
        session.Apply(new PlayerSaveData { stats = new PlayerStatsSaveData { level = 1, attackSpeedUpgradeLevel = 4 }, gold = 1000 });
        Bind();

        Assert.AreEqual("10.00 → 10.00", Row(UpgradeStatType.AttackSpeed).Value, "하한(0.1초)에서는 능력치가 더 오르지 않는다");
        Assert.AreEqual("Lv.5", Row(UpgradeStatType.AttackSpeed).Level);
        Assert.AreEqual("160", Row(UpgradeStatType.AttackSpeed).Cost);

        view.TapUpgrade(UpgradeStatType.AttackSpeed);

        Assert.AreEqual("Lv.6", Row(UpgradeStatType.AttackSpeed).Level, "능력치가 그대로여도 단계는 따라온다");
        Assert.AreEqual("320", Row(UpgradeStatType.AttackSpeed).Cost);
    }

    [Test]
    public void MaxedRow_ShowsMaxAndOnlyCurrentValue()
    {
        upgradeConfig.maxUpgradeLevel = 2;
        session.Apply(new PlayerSaveData { stats = new PlayerStatsSaveData { level = 1, hpUpgradeLevel = 2 }, gold = 1e9 });
        Bind();

        Assert.AreEqual("90", Row(UpgradeStatType.Hp).Value);
        Assert.AreEqual("MAX", Row(UpgradeStatType.Hp).Cost);
        Assert.IsFalse(Row(UpgradeStatType.Hp).CanUpgrade, "골드가 많아도 최대 단계는 살 수 없다");
        Assert.IsTrue(Row(UpgradeStatType.Attack).CanUpgrade);
    }

    [Test]
    public void LevelUpTap_LevelsUp_AndRedrawsHeaderAndRows()
    {
        Bind();
        session.Stats.AddExp(150);

        Assert.IsTrue(view.LevelUpAvailable);
        Assert.AreEqual(1f, view.ExpFill, "막대는 1에서 멈춘다");
        Assert.AreEqual("150.00%", view.ExpPercent);

        view.TapLevelUp();

        Assert.AreEqual("Lv. 2", view.Level);
        Assert.AreEqual("EXP 50 / 200", view.ExpText);
        Assert.IsFalse(view.LevelUpAvailable);
        Assert.AreEqual("70 → 90", Row(UpgradeStatType.Hp).Value, "레벨 기본 HP가 바뀐다");
    }

    [Test]
    public void ApplyingOtherProgress_RedrawsEverything()
    {
        Bind();
        session.Apply(new PlayerSaveData
        {
            stats = new PlayerStatsSaveData { level = 3, currentExp = 30, hpUpgradeLevel = 1, attackUpgradeLevel = 2 },
            gold = 100,
        });

        Assert.AreEqual("Lv. 3", view.Level);
        Assert.AreEqual("EXP 30 / 300", view.ExpText);
        Assert.AreEqual("110 → 130", Row(UpgradeStatType.Hp).Value);
        Assert.AreEqual("Lv.3", Row(UpgradeStatType.Attack).Level);
        Assert.AreEqual("24 → 27", Row(UpgradeStatType.Attack).Value);
        Assert.IsTrue(Row(UpgradeStatType.Hp).CanUpgrade, "비용 100 ≤ 골드 100");
    }

    [Test]
    public void FormatStat_ShortensBigNumbers_AndShowsAttackRate()
    {
        var combat = new PlayerCombatStats(GameNumber.FromDouble(1234567), GameNumber.FromDouble(1e30), 0.5f);

        Assert.AreEqual("1.2M", CharacterTabPresenter.FormatStat(UpgradeStatType.Hp, combat));
        Assert.AreEqual(NumberFormatUtility.Format(GameNumber.FromDouble(1e30)), CharacterTabPresenter.FormatStat(UpgradeStatType.Attack, combat));
        StringAssert.DoesNotContain("E", CharacterTabPresenter.FormatStat(UpgradeStatType.Attack, combat), "지수 표기로 새지 않는다");
        Assert.AreEqual("2.00", CharacterTabPresenter.FormatStat(UpgradeStatType.AttackSpeed, combat));
    }

    [Test]
    public void Dispose_StopsFollowingAndListening()
    {
        Bind();
        presenter.Dispose();
        int calls = view.RowCalls;

        session.Wallet.AddGold((GameNumber)500);
        session.Stats.AddExp(150);

        Assert.AreEqual(calls, view.RowCalls);
        Assert.IsFalse(view.LevelUpAvailable);
        Assert.IsFalse(view.HasListeners);
        presenter = null;
    }
}
