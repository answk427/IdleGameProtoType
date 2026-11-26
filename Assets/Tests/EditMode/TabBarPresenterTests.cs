using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// 탭바 Presenter(docs/UI_DESIGN.md 6-4, 10-6): 진짜 WindowManager + 가짜 로더 + 가짜 View.
// - 창이 연결되고 해금된 탭만 열린다. 잠긴 탭은 이유를 알린다(내용 없음 = 준비 중, 해금 전 = 조건). 같은 탭을 다시 누르면 화면을 비운다.
// - 열려 있던 탭이 잠기면 화면을 비운다. 레드닷은 해금된 탭에만.
// - 지금 탭은 연 탭 창의 핸들로 따라온다: 뒤로가기, 로드 실패, 다른 탭으로 바꾸기. 바꾸는 도중에 "탭 없음"이 끼지 않는다.
[TestFixture]
public class TabBarPresenterTests
{
    private sealed class FakeView : ITabBarView
    {
        public readonly List<UITabType> TabList = new List<UITabType>();
        public readonly Dictionary<UITabType, bool> Locked = new Dictionary<UITabType, bool>();
        public readonly Dictionary<UITabType, bool> Badges = new Dictionary<UITabType, bool>();
        public readonly List<UITabType> Selected = new List<UITabType>(); // SetSelected가 받은 값 전부

        public event Action<UITabType> TabTapped;

        public IReadOnlyList<UITabType> Tabs => TabList;

        public void SetLocked(UITabType tab, bool locked) => Locked[tab] = locked;
        public void SetSelected(UITabType tab) => Selected.Add(tab);
        public void SetBadge(UITabType tab, bool visible) => Badges[tab] = visible;

        public void Tap(UITabType tab) => TabTapped?.Invoke(tab);
        public bool HasListeners => TabTapped != null;
    }

    private sealed class FakePresenter : IWindowPresenter
    {
        public void Tick() { }
        public void Dispose() { }
    }

    private GameObject root;
    private readonly List<GameObject> prefabObjects = new List<GameObject>();
    private FakeWindowLoader loader;
    private WindowManager windows;
    private FakeView view;
    private TabBarPresenter presenter;
    private FakeToasts toasts;
    private ReactiveProperty<bool> skillUnlocked;
    private ReactiveProperty<bool> skillBadge;
    private readonly List<WindowHandle> opened = new List<WindowHandle>();

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("UIRootForTest", typeof(RectTransform));
        root.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        loader = new FakeWindowLoader();
        AddPrefab<TestTabAWindow>();
        AddPrefab<TestTabBWindow>();
        AddPrefab<TestPanelWindow>();
        AddPrefab<TestPopupAWindow>();
        AddPrefab<TestSystemWindow>();

        windows = new WindowManager(root.transform, loader, new InputBlocker(root.transform));
        windows.Bind<TestTabAWindow, TestTabAArgs>((_, __, ___) => new FakePresenter());
        windows.Bind<TestTabBWindow, TestTabBArgs>((_, __, ___) => new FakePresenter());
        windows.Bind<TestPanelWindow, TestPanelArgs>((_, __, ___) => new FakePresenter());
        windows.Bind<TestPopupAWindow, TestPopupAArgs>((_, __, ___) => new FakePresenter());
        windows.Bind<TestSystemWindow, TestSystemArgs>((_, __, ___) => new FakePresenter());

        view = new FakeView();
        view.TabList.AddRange(new[] { UITabType.Character, UITabType.Skill, UITabType.Shop });

        // Character → 탭 A(조건 없음), Skill → 탭 B(레벨 2부터). Shop은 창이 없다. 해금·레드닷 상태는 시험이 직접 바꾼다.
        skillUnlocked = new ReactiveProperty<bool>(true);
        skillBadge = new ReactiveProperty<bool>(false);
        var tabs = new Dictionary<UITabType, TabEntry>
        {
            [UITabType.Character] = new TabEntry(() => Track(windows.Open<TestTabAWindow>(new TestTabAArgs())),
                new ReactiveProperty<bool>(true), () => UnlockCondition.None),
            [UITabType.Skill] = new TabEntry(() => Track(windows.Open<TestTabBWindow>(new TestTabBArgs())),
                skillUnlocked, () => new UnlockCondition(UnlockConditionType.PlayerLevel, 2), skillBadge),
        };
        toasts = new FakeToasts();
        presenter = new TabBarPresenter(view, tabs, () => windows.CloseBelow(UISortBand.System), toasts);
    }

    [TearDown]
    public void TearDown()
    {
        presenter.Dispose();
        windows.Dispose();
        Object.DestroyImmediate(root);
        foreach (GameObject prefab in prefabObjects) Object.DestroyImmediate(prefab);
        prefabObjects.Clear();
        opened.Clear();
    }

    private void AddPrefab<TWindow>() where TWindow : UIWindow
    {
        var prefab = new GameObject(typeof(TWindow).Name, typeof(RectTransform));
        prefab.SetActive(false);
        loader.Add(prefab.AddComponent<TWindow>());
        prefabObjects.Add(prefab);
    }

    private WindowHandle Track(WindowHandle handle)
    {
        opened.Add(handle);
        return handle;
    }

    private static bool IsClosed(WindowHandle handle) => handle.Closed.GetAwaiter().IsCompleted;

    private static WindowStatus StatusOf(WindowHandle handle)
    {
        var awaiter = handle.Closed.GetAwaiter();
        Assert.IsTrue(awaiter.IsCompleted, "창이 아직 닫히지 않았습니다.");
        return awaiter.GetResult();
    }

    private UITabType Current => presenter.CurrentTab.Value;

    // ── 잠금 ──

    [Test]
    public void OnlyTabsWithWindow_AreUnlocked()
    {
        Assert.IsFalse(view.Locked[UITabType.Character]);
        Assert.IsFalse(view.Locked[UITabType.Skill]);
        Assert.IsTrue(view.Locked[UITabType.Shop], "창이 없는 탭은 잠긴다");
        CollectionAssert.AreEqual(new[] { UITabType.None }, view.Selected, "처음에는 선택 없음");
    }

    [Test]
    public void LockedTab_DoesNotOpen_AndSaysNotReady()
    {
        view.Tap(UITabType.Character);
        view.Tap(UITabType.Shop);

        Assert.AreEqual(UITabType.Character, Current, "잠긴 탭은 지금 탭을 바꾸지 않는다");
        Assert.AreEqual(1, opened.Count);
        Assert.IsFalse(IsClosed(opened[0]), "잠긴 탭을 눌러도 화면이 비지 않는다");
        CollectionAssert.AreEqual(new[] { TabBarPresenter.NotReadyMessage }, toasts.Shown, "창이 없는 탭은 준비 중이라고 알린다");
    }

    // ── 해금 (게임 쪽 상태를 구독) ──

    [Test]
    public void NotYetUnlockedTab_IsLocked_AndTellsTheCondition()
    {
        skillUnlocked.Value = false;
        Assert.IsTrue(view.Locked[UITabType.Skill]);

        view.Tap(UITabType.Skill);

        Assert.AreEqual(0, opened.Count, "잠긴 탭은 열지 않는다");
        CollectionAssert.AreEqual(new[] { "Lv.2부터 열립니다" }, toasts.Shown);

        skillUnlocked.Value = true;
        Assert.IsFalse(view.Locked[UITabType.Skill], "해금되면 바로 풀린다");
        view.Tap(UITabType.Skill);
        Assert.AreEqual(UITabType.Skill, Current);
    }

    // 진행도가 내려가 열린 탭이 잠기면(개발자 도구, 롤백) 그 화면을 비운다: 잠긴 탭의 창이 열려 있지 않다.
    [Test]
    public void OpenTab_BecomingLocked_ClosesTheScreen()
    {
        view.Tap(UITabType.Skill);
        WindowHandle popup = windows.Open<TestPopupAWindow>(new TestPopupAArgs());

        skillUnlocked.Value = false;

        Assert.IsTrue(IsClosed(opened[0]));
        Assert.IsTrue(IsClosed(popup), "탭 위의 창도 같이(화면 이동)");
        Assert.AreEqual(UITabType.None, Current);
    }

    [Test]
    public void OtherTab_BecomingLocked_LeavesCurrentTabOpen()
    {
        view.Tap(UITabType.Character);
        skillUnlocked.Value = false;

        Assert.IsFalse(IsClosed(opened[0]));
        Assert.AreEqual(UITabType.Character, Current);
    }

    [Test]
    public void FormatLocked_CoversEveryConditionType()
    {
        Assert.AreEqual("Lv.5부터 열립니다", TabBarPresenter.FormatLocked(new UnlockCondition(UnlockConditionType.PlayerLevel, 5)));
        Assert.AreEqual("스테이지 3부터 열립니다", TabBarPresenter.FormatLocked(new UnlockCondition(UnlockConditionType.StageReached, 3)));
        Assert.AreEqual(TabBarPresenter.NotReadyMessage, TabBarPresenter.FormatLocked(UnlockCondition.None), "조건 없이 잠김 = 데이터 오류");
    }

    // ── 레드닷 ──

    [Test]
    public void Badge_FollowsCondition_OnlyWhileUnlocked()
    {
        Assert.IsFalse(view.Badges[UITabType.Skill]);
        Assert.IsFalse(view.Badges[UITabType.Character], "조건이 없는 탭은 점이 없다");
        Assert.IsFalse(view.Badges[UITabType.Shop]);

        skillBadge.Value = true;
        Assert.IsTrue(view.Badges[UITabType.Skill]);

        skillUnlocked.Value = false;
        Assert.IsFalse(view.Badges[UITabType.Skill], "잠긴 탭에는 점을 그리지 않는다");

        skillUnlocked.Value = true;
        Assert.IsTrue(view.Badges[UITabType.Skill]);

        skillBadge.Value = false;
        Assert.IsFalse(view.Badges[UITabType.Skill]);
    }

    // ── 열기 / 바꾸기 / 토글 ──

    [Test]
    public void Tap_OpensTab_AndSelectsIt()
    {
        view.Tap(UITabType.Character);

        Assert.AreEqual(UITabType.Character, Current);
        Assert.IsFalse(IsClosed(opened[0]));
        Assert.AreEqual(UITabType.Character, view.Selected[view.Selected.Count - 1]);
    }

    [Test]
    public void TapOtherTab_ClosesPrevious_WithoutPassingThroughNone()
    {
        view.Tap(UITabType.Character);
        view.Tap(UITabType.Skill);

        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(opened[0]), "이전 탭은 Exclusive가 닫는다");
        Assert.IsFalse(IsClosed(opened[1]));
        Assert.AreEqual(UITabType.Skill, Current);
        CollectionAssert.AreEqual(new[] { UITabType.None, UITabType.Character, UITabType.Skill }, view.Selected,
            "바꾸는 도중에 '탭 없음'이 끼지 않는다");
    }

    [Test]
    public void TapSameTab_ClearsScreenBelowSystem()
    {
        WindowHandle system = windows.Open<TestSystemWindow>(new TestSystemArgs());
        view.Tap(UITabType.Character);
        WindowHandle popup = windows.Open<TestPopupAWindow>(new TestPopupAArgs()); // 탭 위에 뜬 창

        view.Tap(UITabType.Character);

        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(opened[0]));
        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(popup), "탭 위에 뜬 창도 같이 닫힌다(탭 전환과 같은 화면 이동)");
        Assert.IsFalse(IsClosed(system), "System 창은 남는다");
        Assert.AreEqual(UITabType.None, Current);

        view.Tap(UITabType.Character);
        Assert.AreEqual(UITabType.Character, Current, "다시 누르면 다시 열린다");
        Assert.IsFalse(IsClosed(opened[1]));
    }

    // ── 밖에서 닫힘 ──

    [Test]
    public void BackClosesTab_CurrentFollows()
    {
        view.Tap(UITabType.Skill);
        windows.Back();

        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(opened[0]));
        Assert.AreEqual(UITabType.None, Current);
        Assert.AreEqual(UITabType.None, view.Selected[view.Selected.Count - 1]);

        view.Tap(UITabType.Skill);
        Assert.AreEqual(UITabType.Skill, Current, "뒤로가기로 닫은 탭을 누르면 다시 열린다(닫히지 않는다)");
    }

    [Test]
    public void OtherWindowClearingScreen_CurrentFollows()
    {
        view.Tap(UITabType.Character);
        windows.Open<TestPanelWindow>(new TestPanelArgs());
        windows.CloseBelow(UISortBand.System);

        Assert.AreEqual(UITabType.None, Current);
    }

    [Test]
    public void FailedOpen_KeepsPreviousTab()
    {
        view.Tap(UITabType.Character);

        loader.Defer<TestTabBWindow>();
        view.Tap(UITabType.Skill);
        Assert.AreEqual(UITabType.Skill, Current, "요청한 탭이 지금 탭(로드 중)");
        Assert.IsFalse(IsClosed(opened[0]), "새 탭이 보이기 전까지 이전 탭은 그대로");

        LogAssert.Expect(LogType.Error, new Regex("TestTabBWindow 프리팹이 없습니다"));
        loader.FinishMissing<TestTabBWindow>();

        Assert.AreEqual(WindowStatus.Failed, StatusOf(opened[1]));
        Assert.IsFalse(IsClosed(opened[0]));
        Assert.AreEqual(UITabType.Character, Current, "열지 못하면 화면에 남은 탭으로 돌아온다");

        view.Tap(UITabType.Character);
        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(opened[0]), "돌아온 탭을 다시 누르면 토글로 닫힌다");
    }

    [Test]
    public void TapWhileLoading_SameTab_CancelsIt()
    {
        loader.Defer<TestTabAWindow>();
        view.Tap(UITabType.Character);
        view.Tap(UITabType.Character); // 로드 중에 다시 누름(입력 차단이 막지만 Presenter 규칙으로도 맞아야 한다)

        loader.Finish<TestTabAWindow>();

        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(opened[0]), "로드 중에 받은 닫기 기준이 로드가 끝날 때 적용된다");
        Assert.AreEqual(UITabType.None, Current);
    }

    // ── 수명 ──

    [Test]
    public void Dispose_StopsListening()
    {
        presenter.Dispose();

        Assert.IsFalse(view.HasListeners);
        view.Tap(UITabType.Character);
        Assert.AreEqual(0, opened.Count);
    }
}
