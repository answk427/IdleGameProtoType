using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

// UI 상태 사본(개발자 도구의 UI 상태 표시가 읽는 것, docs/UI_DESIGN.md 10-6): 스택 순서 그대로, 로드 중인 창, 딤 자리, 차단 이유.
[TestFixture]
public class UIDebugSnapshotTests
{
    private sealed class FakePresenter : IWindowPresenter
    {
        public void Tick() { }
        public void Dispose() { }
    }

    private GameObject root;
    private readonly List<GameObject> prefabs = new List<GameObject>();
    private FakeWindowLoader loader;
    private InputBlocker blocker;
    private WindowManager windows;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("UIRootForTest", typeof(RectTransform));
        root.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        loader = new FakeWindowLoader();
        Add<TestTabAWindow>();
        Add<TestPopupAWindow>();
        Add<TestSystemWindow>();

        blocker = new InputBlocker(root.transform);
        windows = new WindowManager(root.transform, loader, blocker);
        windows.Bind<TestTabAWindow, TestTabAArgs>((_, __, ___) => new FakePresenter());
        windows.Bind<TestPopupAWindow, TestPopupAArgs>((_, __, ___) => new FakePresenter());
        windows.Bind<TestSystemWindow, TestSystemArgs>((_, __, ___) => new FakePresenter());
    }

    [TearDown]
    public void TearDown()
    {
        windows.Dispose();
        Object.DestroyImmediate(root);
        foreach (GameObject prefab in prefabs) Object.DestroyImmediate(prefab);
        prefabs.Clear();
    }

    private void Add<TWindow>() where TWindow : UIWindow
    {
        var prefab = new GameObject(typeof(TWindow).Name, typeof(RectTransform));
        prefab.SetActive(false);
        loader.Add(prefab.AddComponent<TWindow>());
        prefabs.Add(prefab);
    }

    private UIDebugSnapshot Take()
    {
        var snapshot = new UIDebugSnapshot();
        windows.Describe(snapshot);
        blocker.Describe(snapshot);
        return snapshot;
    }

    [Test]
    public void Snapshot_FollowsTheStack_LoadingWindowOnTop_DimUnderTopModal()
    {
        windows.Open<TestTabAWindow>(new TestTabAArgs());
        windows.Open<TestPopupAWindow>(new TestPopupAArgs());
        loader.Defer<TestSystemWindow>();
        windows.Open<TestSystemWindow>(new TestSystemArgs());

        UIDebugSnapshot s = Take();

        Assert.AreEqual(3, s.Windows.Count);
        Assert.AreEqual(nameof(TestTabAWindow), s.Windows[0].Name);
        Assert.AreEqual(UISortBand.Window, s.Windows[0].Band);
        Assert.AreEqual((int)UISortBand.Window, s.Windows[0].Order);
        Assert.IsTrue(s.Windows[0].IsOpen);
        Assert.IsFalse(s.Windows[0].Modal);

        Assert.AreEqual(nameof(TestPopupAWindow), s.Windows[1].Name);
        Assert.AreEqual((int)UISortBand.Popup, s.Windows[1].Order);
        Assert.IsTrue(s.Windows[1].Modal);

        Assert.AreEqual(nameof(TestSystemWindow), s.Windows[2].Name, "로드 중인 창은 맨 위");
        Assert.IsFalse(s.Windows[2].IsLoaded);
        Assert.AreEqual((int)UISortBand.Popup - 1, s.DimOrder, "딤은 맨 위 모달(로드된 것) 바로 아래");

        Assert.AreEqual(1, s.Blocks.Count, "로드 중인 창이 입력을 막고 있다");
        StringAssert.Contains(nameof(TestSystemWindow), s.Blocks[0].Reason);

        loader.Finish<TestSystemWindow>();
        s = Take();
        Assert.AreEqual(nameof(TestSystemWindow), s.Windows[2].Name);
        Assert.IsTrue(s.Windows[2].IsLoaded);
        Assert.AreEqual((int)UISortBand.System - 1, s.DimOrder);
        Assert.AreEqual(0, s.Blocks.Count, "로드가 끝나면 풀린다");
    }

    [Test]
    public void Snapshot_Empty_WhenNothingOpen()
    {
        UIDebugSnapshot s = Take();
        Assert.AreEqual(0, s.Windows.Count);
        Assert.IsNull(s.DimOrder);
        Assert.AreEqual(0, s.Blocks.Count);
    }
}
