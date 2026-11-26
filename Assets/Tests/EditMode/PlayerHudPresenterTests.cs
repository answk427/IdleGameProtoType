using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json;
using UnityEngine;

// 플레이어 HUD Presenter(docs/COMBAT_ARCHITECTURE.md 10-10): 가짜 View로 표시 규칙을 본다.
// 진행도·HP·골드를 따라가고(진행도 교체 포함), 경험치 막대를 0~1로 자르고, 보스전 남은 시간은 제한이 있을 때만,
// 글자는 보이는 초가 바뀔 때만 만든다.
[TestFixture]
public class PlayerHudPresenterTests
{
    private sealed class FakeView : IPlayerHudView
    {
        public bool Visible;
        public float HpRatio, ExpRatio;
        public string HpText, Level, Gold, TimerText;
        public bool TimerVisible, TimerUrgent;
        public int TimerCalls;

        public void SetVisible(bool visible) => Visible = visible;
        public void SetHp(float ratio, string text) { HpRatio = ratio; HpText = text; }
        public void SetExp(float ratio) => ExpRatio = ratio;
        public void SetLevel(string text) => Level = text;
        public void SetGold(string text) => Gold = text;

        public void SetBossTimer(bool visible, string text, bool urgent)
        {
            TimerCalls++;
            TimerVisible = visible;
            TimerText = text;
            TimerUrgent = urgent;
        }
    }

    private PlayerStatDatabase statDatabase;
    private PlayerUpgradeConfig upgradeConfig;
    private PlayerStats stats;
    private GoldWallet wallet;
    private ReactiveProperty<HpState> hp;
    private FakeView view;
    private double deadline;
    private double now;

    private static PlayerStatData MakeLevel(int level, int requiredExp) =>
        JsonConvert.DeserializeObject<PlayerStatData>(
            $"{{\"level\":{level},\"requiredExp\":{requiredExp},\"baseMaxHp\":100,\"baseAttackDamage\":10,\"baseAttackInterval\":1.0}}");

    [SetUp]
    public void SetUp()
    {
        statDatabase = ScriptableObject.CreateInstance<PlayerStatDatabase>();
        statDatabase.entries.Add(MakeLevel(1, 100));
        statDatabase.entries.Add(MakeLevel(2, 200));
        statDatabase.entries.Add(MakeLevel(3, 300));
        upgradeConfig = ScriptableObject.CreateInstance<PlayerUpgradeConfig>();

        stats = new PlayerStats();
        stats.Initialize(statDatabase, upgradeConfig);
        wallet = new GoldWallet();
        hp = new ReactiveProperty<HpState>(new HpState((GameNumber)80, (GameNumber)100));
        view = new FakeView();
        deadline = double.PositiveInfinity;
        now = 0.0;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(statDatabase);
        Object.DestroyImmediate(upgradeConfig);
    }

    private PlayerHudPresenter Bind() => new PlayerHudPresenter(view, stats, wallet, hp, () => deadline, () => now);

    [Test]
    public void Bind_DrawsCurrentValues_AndShowsHud()
    {
        wallet.AddGold((GameNumber)1500);
        Bind();

        Assert.IsTrue(view.Visible);
        Assert.AreEqual("Lv.1", view.Level);
        Assert.AreEqual(NumberFormatUtility.Format((GameNumber)1500), view.Gold);
        Assert.AreEqual(0.8f, view.HpRatio, 1e-6f);
        Assert.AreEqual($"{NumberFormatUtility.Format((GameNumber)80)} / {NumberFormatUtility.Format((GameNumber)100)}", view.HpText);
        Assert.AreEqual(0f, view.ExpRatio);
        Assert.IsFalse(view.TimerVisible, "처음에는 보스 시간이 숨겨져 있다");
    }

    [Test]
    public void FollowsProgress_IncludingWholeReplace()
    {
        Bind();

        stats.AddExp(50);
        Assert.AreEqual(0.5f, view.ExpRatio, 1e-6f);

        stats.LoadSave(new PlayerStatsSaveData { level = 3, currentExp = 30 });
        wallet.LoadGold((GameNumber)9e12);
        hp.Value = new HpState((GameNumber)0, (GameNumber)100);

        Assert.AreEqual("Lv.3", view.Level);
        Assert.AreEqual(1f, view.ExpRatio, "최대 레벨이면 가득");
        Assert.AreEqual(NumberFormatUtility.Format((GameNumber)9e12), view.Gold);
        Assert.AreEqual(0f, view.HpRatio);
    }

    [TestCase(150, 100, false, 1f)]   // 수동 레벨업이라 필요량을 넘어 머문다
    [TestCase(50, 100, false, 0.5f)]
    [TestCase(0, 100, false, 0f)]
    [TestCase(-5, 100, false, 0f)]
    [TestCase(10, 0, false, 1f)]      // 필요량 0(데이터 오류)
    [TestCase(5, 100, true, 1f)]      // 최대 레벨
    public void ExpRatio_IsClampedToZeroOne(int exp, int required, bool isMaxLevel, float expected)
    {
        Assert.AreEqual(expected, PlayerHudPresenter.ExpRatio(exp, required, isMaxLevel), 1e-6f);
    }

    [Test]
    public void BossTimer_HiddenWithoutLimit_AndNoRedrawEachFrame()
    {
        PlayerHudPresenter presenter = Bind();
        int callsAfterBind = view.TimerCalls;

        for (int i = 0; i < 100; i++) presenter.Tick();

        Assert.IsFalse(view.TimerVisible);
        Assert.AreEqual(callsAfterBind, view.TimerCalls, "제한이 없으면 매 프레임 다시 그리지 않는다");
    }

    [Test]
    public void BossTimer_ShowsCeilSeconds_RedrawsOnlyWhenSecondChanges_AndTurnsUrgent()
    {
        PlayerHudPresenter presenter = Bind();
        deadline = 100.0;

        now = 73.7; // 26.3초 남음 → 0:27
        presenter.Tick();
        Assert.IsTrue(view.TimerVisible);
        Assert.AreEqual("보스 0:27", view.TimerText);
        Assert.IsFalse(view.TimerUrgent);
        int calls = view.TimerCalls;

        now = 73.9; // 아직 27초
        presenter.Tick();
        Assert.AreEqual(calls, view.TimerCalls, "보이는 초가 같으면 다시 그리지 않는다");

        now = 90.0; // 10초 → 강조
        presenter.Tick();
        Assert.AreEqual("보스 0:10", view.TimerText);
        Assert.IsTrue(view.TimerUrgent);

        now = 105.0; // 지남 → 0:00
        presenter.Tick();
        Assert.AreEqual("보스 0:00", view.TimerText);

        deadline = double.PositiveInfinity; // 보스전이 끝남
        presenter.Tick();
        Assert.IsFalse(view.TimerVisible);
    }

    [Test]
    public void FormatBossTime_UsesMinutesAndSeconds()
    {
        Assert.AreEqual("보스 1:15", PlayerHudPresenter.FormatBossTime(75));
        Assert.AreEqual("보스 0:05", PlayerHudPresenter.FormatBossTime(5));
        Assert.AreEqual("보스 10:00", PlayerHudPresenter.FormatBossTime(600));
    }

    [Test]
    public void Dispose_StopsFollowing()
    {
        PlayerHudPresenter presenter = Bind();
        presenter.Dispose();

        stats.LoadSave(new PlayerStatsSaveData { level = 2 });

        Assert.AreEqual("Lv.1", view.Level);
    }
}
