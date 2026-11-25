using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json;
using UnityEngine;

// 실행 중 진행도 교체(GameSession.Apply → PlayerStats.LoadSave)의 불변식(docs/DEV_TOOLS.md T6).
//   진행도 A로 돌던 세션에 B를 적용한 뒤의 상태 = B로 새로 시작한 세션의 상태
// 그리고 교체는 "다시 읽어라"(OnReloaded)만 알리고, 저장을 일으키는 사건은 내지 않는다.
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

    // 저장되는 값 전부와, 그 값에서 계산되는 값(캐릭터 능력치·HUD가 읽는 값)을 한 줄로.
    private static string Fingerprint(GameSession s)
    {
        PlayerStats st = s.Stats;
        return JsonUtility.ToJson(s.ToSaveData()) +
               $"|lv {st.Level} exp {st.CurrentExp}/{st.RequiredExp} canLv {st.CanLevelUp}" +
               $"|hp {st.MaxHp} atk {st.AttackDamage} int {st.AttackInterval:R}" +
               $"|learned {string.Join(",", st.LearnedSkills.Value)} slots {string.Join(",", st.EquippedSlots.Value)}" +
               $"|gold {s.Wallet.Gold}";
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
    public void Apply_RaisesReloadOnly_NotSavingEvents()
    {
        GameSession session = NewSession(ProgressA());
        int reloaded = 0, saving = 0, goldChanged = 0;
        session.Stats.OnReloaded += () => reloaded++;
        session.Stats.OnUpgraded += () => saving++;
        session.Stats.OnSkillLearned += () => saving++;
        session.Stats.OnSkillEquipChanged += () => saving++;
        session.Wallet.OnGoldChanged += _ => goldChanged++;

        session.Apply(ProgressB());

        Assert.AreEqual(1, reloaded, "다시 읽으라는 알림은 한 번");
        Assert.AreEqual(0, saving, "저장을 일으키는 사건은 내지 않는다");
        Assert.AreEqual(1, goldChanged, "골드는 지갑이 알린다");
    }

    // 받는 쪽이 OnReloaded 안에서 읽는 값은 이미 새 값이어야 한다(알림이 데이터보다 먼저 나가면 HUD가 옛 값을 그린다).
    [Test]
    public void ReloadNotification_SeesNewValues()
    {
        GameSession session = NewSession(ProgressA());
        int levelSeen = -1;
        GameNumber hpSeen = GameNumber.Zero;
        IReadOnlyList<int> slotsSeen = null;
        session.Stats.OnReloaded += () =>
        {
            levelSeen = session.Stats.Level;
            hpSeen = session.Stats.MaxHp;
            slotsSeen = session.Stats.EquippedSlots.Value;
        };

        session.Apply(ProgressB());

        Assert.AreEqual(3, levelSeen);
        Assert.AreEqual(GameNumber.FromDouble(90 + 7 * 20), hpSeen);
        CollectionAssert.AreEqual(new[] { -1, 5, -1, 2, 1 }, slotsSeen);
    }

    // 세션은 넘겨받은 객체를 그대로 붙잡는다(값만 덮어쓰기). 그래서 GameManager.LoadSnapshot은 JSON으로 한 번 복사해서 넘긴다.
    // 이 테스트는 그 전제(복사하지 않으면 공유된다)를 고정해, 전제가 바뀌면 LoadSnapshot의 복사도 다시 보게 한다.
    [Test]
    public void Apply_KeepsReferenceToGivenData_SoCallersMustCopy()
    {
        PlayerSaveData given = ProgressB();
        GameSession session = NewSession(ProgressA());
        session.Apply(given);

        given.stats.level = 1;

        Assert.AreEqual(1, session.Stats.Level);
    }
}
