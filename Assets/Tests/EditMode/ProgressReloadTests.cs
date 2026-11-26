using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json;
using UnityEngine;

// 실행 중 진행도 교체(GameSession.Apply → PlayerStats.LoadSave)의 불변식(docs/DEV_TOOLS.md T6, docs/COMBAT_ARCHITECTURE.md 10-10).
//   진행도 A로 돌던 세션에 B를 적용한 뒤의 상태 = B로 새로 시작한 세션의 상태
// 교체는 값만 덮어쓴다. 상태가 바뀌므로 구독하는 쪽이 따라오고("전부 다시 읽기" 사건 OnReloaded는 없어졌다),
// 저장을 일으키는 사건은 내지 않는다. 파생 상태에는 중간 조합(새 레벨 + 옛 업그레이드)이 보이지 않는다.
[TestFixture]
public class ProgressReloadTests
{
    private PlayerStatDatabase statDatabase;
    private PlayerUpgradeConfig upgradeConfig;

    private static PlayerStatData MakeLevel(int level, int requiredExp, int baseMaxHp, int baseAttackDamage, float interval)
    {
        string json = "{" +
            $"\"level\":{level}," +
            $"\"requiredExp\":{requiredExp}," +
            $"\"baseMaxHp\":{baseMaxHp}," +
            $"\"baseAttackDamage\":{baseAttackDamage}," +
            $"\"baseAttackInterval\":{interval.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            "}";
        return JsonConvert.DeserializeObject<PlayerStatData>(json);
    }

    [SetUp]
    public void SetUp()
    {
        statDatabase = ScriptableObject.CreateInstance<PlayerStatDatabase>();
        statDatabase.entries.Add(MakeLevel(1, requiredExp: 100, baseMaxHp: 50, baseAttackDamage: 10, interval: 1.0f));
        statDatabase.entries.Add(MakeLevel(2, requiredExp: 200, baseMaxHp: 70, baseAttackDamage: 14, interval: 0.9f));
        statDatabase.entries.Add(MakeLevel(3, requiredExp: 300, baseMaxHp: 90, baseAttackDamage: 18, interval: 0.8f));

        upgradeConfig = ScriptableObject.CreateInstance<PlayerUpgradeConfig>();
        upgradeConfig.settings.hpPerUpgrade = 20;
        upgradeConfig.settings.attackPerUpgrade = 3;
        upgradeConfig.settings.attackSpeedPerUpgrade = 0.05f;
        upgradeConfig.maxUpgradeLevel = 0;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(statDatabase);
        Object.DestroyImmediate(upgradeConfig);
    }

    private GameSession NewSession(PlayerSaveData data)
    {
        var session = new GameSession(statDatabase, upgradeConfig);
        session.Apply(data);
        return session;
    }

    private static PlayerSaveData ProgressA() => new PlayerSaveData
    {
        gold = 123,
        currentStageNumber = 1,
        stats = new PlayerStatsSaveData
        {
            level = 1,
            currentExp = 40,
            hpUpgradeLevel = 0,
            attackUpgradeLevel = 2,
            attackSpeedUpgradeLevel = 0,
            learnedSkillIds = new List<int> { 1 },
            equippedSkillSlotIds = new[] { 1, -1, -1, -1, -1 },
        },
    };

    private static PlayerSaveData ProgressB() => new PlayerSaveData
    {
        gold = 9.5e12,
        currentStageNumber = 3,
        stats = new PlayerStatsSaveData
        {
            level = 3,
            currentExp = 250,
            hpUpgradeLevel = 7,
            attackUpgradeLevel = 0,
            attackSpeedUpgradeLevel = 4,
            learnedSkillIds = new List<int> { 1, 2, 5 },
            equippedSkillSlotIds = new[] { -1, 5, -1, 2, 1 },
        },
    };

    // 저장되는 값 전부와, 상태·파생 상태(캐릭터·HUD·탭이 구독하는 값)를 한 줄로.
    private static string Fingerprint(GameSession s)
    {
        PlayerStats st = s.Stats;
        return JsonUtility.ToJson(s.ToSaveData()) +
               $"|lv {st.Level.Value} exp {st.Exp.Value}/{st.RequiredExp.Value} canLv {st.CanLevelUp.Value} max {st.IsMaxLevel.Value}" +
               $"|up {st.UpgradeLevel(UpgradeStatType.Hp).Value},{st.UpgradeLevel(UpgradeStatType.Attack).Value},{st.UpgradeLevel(UpgradeStatType.AttackSpeed).Value}" +
               $"|{st.CombatStats.Value}" +
               $"|learned {string.Join(",", st.LearnedSkills.Value)} slots {string.Join(",", st.EquippedSlots.Value)}" +
               $"|gold {s.Wallet.Gold.Value}";
    }

    [Test]
    public void ApplyWhileRunning_EqualsFreshStartWithSameProgress()
    {
        GameSession running = NewSession(ProgressA());
        running.Wallet.AddGold(GameNumber.FromDouble(77)); // 돌던 중의 변화
        running.Stats.AddExp(10);

        running.Apply(ProgressB());

        Assert.AreEqual(Fingerprint(NewSession(ProgressB())), Fingerprint(running));
    }

    [Test]
    public void ApplyBackAndForth_IsStable()
    {
        GameSession session = NewSession(ProgressA());
        string a = Fingerprint(session);

        session.Apply(ProgressB());
        session.Apply(ProgressA());

        Assert.AreEqual(a, Fingerprint(session));
    }

    [Test]
    public void Apply_StatesFollow_AndRaisesNoSavingEvents()
    {
        GameSession session = NewSession(ProgressA());
        int saving = 0;
        session.Stats.LevelReached.Subscribe(_ => saving++);
        session.Stats.Upgraded.Subscribe(_ => saving++);
        session.Stats.SkillLearned.Subscribe(_ => saving++);
        session.Stats.SkillEquipChanged.Subscribe(_ => saving++);
        int levelSeen = -1;
        GameNumber goldSeen = GameNumber.Zero;
        IReadOnlyList<int> slotsSeen = null;
        session.Stats.Level.Subscribe(v => levelSeen = v);
        session.Wallet.Gold.Subscribe(v => goldSeen = v);
        session.Stats.EquippedSlots.Subscribe(v => slotsSeen = v);

        session.Apply(ProgressB());

        Assert.AreEqual(0, saving, "저장을 일으키는 사건은 내지 않는다");
        Assert.AreEqual(3, levelSeen);
        Assert.AreEqual(GameNumber.FromDouble(9.5e12), goldSeen);
        CollectionAssert.AreEqual(new[] { -1, 5, -1, 2, 1 }, slotsSeen);
    }

    // 파생 상태는 저장 데이터에서 계산하므로 교체 도중의 조합(새 레벨 + 옛 업그레이드)이 보이지 않는다.
    // 보이면 최대 HP가 잠깐 줄어 캐릭터의 현재 HP가 깎인 채 남을 수 있다.
    [Test]
    public void Apply_CombatStatsSubscriber_SeesOnlyTheFinalValue()
    {
        GameSession session = NewSession(ProgressA());
        var seen = new List<PlayerCombatStats>();
        bool initial = true;
        session.Stats.CombatStats.Subscribe(v => { if (!initial) seen.Add(v); });
        initial = false;

        session.Apply(ProgressB());

        Assert.AreEqual(1, seen.Count, "중간 조합 없이 한 번");
        Assert.AreEqual(NewSession(ProgressB()).Stats.CombatStats.Value, seen[0]);
        Assert.AreEqual(GameNumber.FromDouble(90 + 7 * 20), seen[0].MaxHp);
    }

    // 세션은 넘겨받은 객체를 그대로 붙잡는다(값만 덮어쓰기). 그래서 GameManager.LoadSnapshot은 JSON으로 한 번 복사해서 넘긴다.
    // 이 테스트는 그 전제(복사하지 않으면 공유된다)를 고정해, 전제가 바뀌면 LoadSnapshot의 복사도 다시 보게 한다.
    [Test]
    public void Apply_KeepsReferenceToGivenData_SoCallersMustCopy()
    {
        PlayerSaveData given = ProgressB();
        GameSession session = NewSession(ProgressA());
        session.Apply(given);

        Assert.AreSame(given.stats, session.Stats.GetSaveData());
    }
}
