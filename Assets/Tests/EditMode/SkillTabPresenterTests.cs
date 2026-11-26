using System;
using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

// 스킬 탭(장착 슬롯 바) Presenter: 진짜 PlayerStats + 가짜 View.
// - 슬롯은 장착 슬롯 상태로 그린다(캐릭터를 거치지 않는다). 장착이 바뀌거나 진행도를 바꾸면 따라온다.
// - 쿨다운은 Tick에서 모든 슬롯을 읽는다. 슬롯 탭은 스킬 사용 요청, 관리 버튼은 스킬 관리 창 열기.
[TestFixture]
public class SkillTabPresenterTests
{
    private sealed class FakeView : ISkillTabView
    {
        public int SlotCount;
        public readonly Dictionary<int, Sprite> Icons = new Dictionary<int, Sprite>(); // 없으면 빈 슬롯
        public readonly Dictionary<int, float> Cooldowns = new Dictionary<int, float>();

        public event Action<int> SlotTapped;
        public event Action ManageTapped;

        public void SetSlotCount(int count) => SlotCount = count;
        public void ShowSlotSkill(int slot, Sprite icon) => Icons[slot] = icon;
        public void ClearSlot(int slot) => Icons.Remove(slot);
        public void SetSlotCooldown(int slot, float ratio) => Cooldowns[slot] = ratio;

        public void TapSlot(int slot) => SlotTapped?.Invoke(slot);
        public void TapManage() => ManageTapped?.Invoke();
        public bool HasListeners => SlotTapped != null || ManageTapped != null;
    }

    private PlayerStatDatabase statDatabase;
    private PlayerUpgradeConfig upgradeConfig;
    private PlayerStats stats;
    private FakeView view;
    private SkillTabPresenter presenter;
    private readonly List<Texture2D> textures = new List<Texture2D>();
    private readonly List<Sprite> sprites = new List<Sprite>();
    private SkillEntry fireball, heal;
    private readonly List<int> requested = new List<int>();
    private int manageOpened;

    [SetUp]
    public void SetUp()
    {
        statDatabase = ScriptableObject.CreateInstance<PlayerStatDatabase>();
        statDatabase.entries.Add(JsonConvert.DeserializeObject<PlayerStatData>(
            "{\"level\":1,\"requiredExp\":100,\"baseMaxHp\":100,\"baseAttackDamage\":10,\"baseAttackInterval\":1.0}"));
        upgradeConfig = ScriptableObject.CreateInstance<PlayerUpgradeConfig>();
        stats = new PlayerStats();
        stats.Initialize(statDatabase, upgradeConfig);

        fireball = TestSkills.CreateEntry(id: 1);
        fireball.icon = MakeSprite();
        heal = TestSkills.CreateEntry(id: 2, effectType: SkillEffectType.Heal);
        heal.icon = MakeSprite();

        // 배운 스킬 1, 2. 슬롯 0 = 1, 슬롯 2 = 2.
        stats.LoadSave(new PlayerStatsSaveData
        {
            learnedSkillIds = new List<int> { 1, 2 },
            equippedSkillSlotIds = new[] { 1, -1, 2, -1, -1 },
        });

        view = new FakeView();
        requested.Clear();
        manageOpened = 0;
    }

    [TearDown]
    public void TearDown()
    {
        presenter?.Dispose();
        presenter = null;
        foreach (Sprite sprite in sprites) Object.DestroyImmediate(sprite);
        foreach (Texture2D texture in textures) Object.DestroyImmediate(texture);
        sprites.Clear();
        textures.Clear();
        Object.DestroyImmediate(statDatabase);
        Object.DestroyImmediate(upgradeConfig);
    }

    private Sprite MakeSprite()
    {
        var texture = new Texture2D(2, 2);
        textures.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
        sprites.Add(sprite);
        return sprite;
    }

    private void Bind(Func<int, float> cooldown = null) =>
        presenter = new SkillTabPresenter(view, stats, new[] { fireball, heal }, cooldown ?? (_ => 0f),
            requested.Add, () => manageOpened++);

    [Test]
    public void Bind_DrawsEquippedSlots()
    {
        Bind();

        Assert.AreEqual(PlayerStatsSaveData.SkillSlotCount, view.SlotCount);
        Assert.AreSame(fireball.icon, view.Icons[0]);
        Assert.AreSame(heal.icon, view.Icons[2]);
        Assert.IsFalse(view.Icons.ContainsKey(1), "빈 슬롯");
        Assert.AreEqual(2, view.Icons.Count);
    }

    [Test]
    public void EquipChange_Redraws()
    {
        Bind();

        stats.EquipSkill(2, 4); // 슬롯 2에 있던 스킬을 4로 옮긴다
        stats.UnequipSkill(0);

        Assert.AreSame(heal.icon, view.Icons[4]);
        Assert.IsFalse(view.Icons.ContainsKey(2), "옮겨 간 자리는 빈다");
        Assert.IsFalse(view.Icons.ContainsKey(0));
    }

    [Test]
    public void ApplyingOtherProgress_Redraws()
    {
        Bind();
        stats.LoadSave(new PlayerStatsSaveData
        {
            learnedSkillIds = new List<int> { 1 },
            equippedSkillSlotIds = new[] { -1, -1, -1, 1, -1 },
        });

        CollectionAssert.AreEquivalent(new[] { 3 }, view.Icons.Keys);
    }

    [Test]
    public void UnknownSkillInSlot_ShowsEmpty()
    {
        stats.LoadSave(new PlayerStatsSaveData
        {
            learnedSkillIds = new List<int> { 99 },
            equippedSkillSlotIds = new[] { 99, -1, -1, -1, -1 },
        });
        Bind();

        Assert.IsEmpty(view.Icons, "데이터에 없는 스킬은 빈 슬롯으로 보인다");
    }

    [Test]
    public void Tick_ReadsCooldownOfEverySlot()
    {
        Bind(slot => slot * 0.1f);
        presenter.Tick();

        Assert.AreEqual(PlayerStatsSaveData.SkillSlotCount, view.Cooldowns.Count);
        for (int slot = 0; slot < PlayerStatsSaveData.SkillSlotCount; slot++)
            Assert.AreEqual(slot * 0.1f, view.Cooldowns[slot]);
    }

    [Test]
    public void Taps_BecomeRequests()
    {
        Bind();

        view.TapSlot(2);
        view.TapSlot(1); // 빈 슬롯도 요청은 보낸다(쓸 스킬이 없으면 캐릭터가 무시한다)
        view.TapManage();

        CollectionAssert.AreEqual(new[] { 2, 1 }, requested);
        Assert.AreEqual(1, manageOpened);
    }

    [Test]
    public void Dispose_StopsFollowingAndListening()
    {
        Bind();
        presenter.Dispose();

        stats.UnequipSkill(0);
        view.TapSlot(0);

        Assert.AreSame(fireball.icon, view.Icons[0], "닫힌 뒤의 변경은 그리지 않는다");
        Assert.IsEmpty(requested);
        Assert.IsFalse(view.HasListeners);
        presenter = null;
    }
}
