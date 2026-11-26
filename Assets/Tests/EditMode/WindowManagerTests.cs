using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// WindowManager의 스택 규칙(docs/UI_DESIGN.md 6-4): 스택 = [로드된 창: 구분 순, 같은 구분은 열린 순] + [로드 중인 창].
// 스택 맨 위가 화면 맨 위다. System 창은 그 위로 열린 Popup에게 뒤로가기를 넘기지 않고, 탭 전환(CloseBelow)으로 닫히지 않는다.
// 로드 중인 창은 구분을 모르므로 맨 위에서 뒤로가기를 먹기만 하고, 로드가 끝나면 자기 자리로 가며,
// 로드 중에 받은 닫기 기준은 로드가 끝날 때 적용된다(구분을 처음부터 알았던 것과 결과가 같다).
//
// 로더는 가짜다: 창마다 로드 완료를 늦출 수 있다. AwaitableCompletionSource의 완료는 그 자리에서 이어지므로
// Finish를 부르면 WindowManager의 열기가 끝까지 진행된다.
[TestFixture]
public class WindowManagerTests
{
    private const int PopupBand = (int)UISortBand.Popup;
    private const int SystemBand = (int)UISortBand.System;
    private const int WindowBand = (int)UISortBand.Window;

    private sealed class FakeLoader : IWindowLoader
    {
        private readonly Dictionary<Type, UIWindow> prefabs = new Dictionary<Type, UIWindow>();
        private readonly HashSet<Type> deferred = new HashSet<Type>();
        private readonly Dictionary<Type, AwaitableCompletionSource<UIWindow>> pending = new Dictionary<Type, AwaitableCompletionSource<UIWindow>>();

        public void Add(UIWindow prefab) => prefabs[prefab.GetType()] = prefab;

        // 다음 로드를 Finish까지 끝내지 않는다.
        public void Defer<TWindow>() where TWindow : UIWindow => deferred.Add(typeof(TWindow));

        public void Finish<TWindow>() where TWindow : UIWindow
        {
            AwaitableCompletionSource<UIWindow> source = pending[typeof(TWindow)];
            pending.Remove(typeof(TWindow));
            source.SetResult(prefabs[typeof(TWindow)]);
        }

        public Awaitable<UIWindow> LoadAsync(Type windowType, CancellationToken cancellationToken)
        {
            var source = new AwaitableCompletionSource<UIWindow>();
            Awaitable<UIWindow> awaitable = source.Awaitable;

            if (deferred.Remove(windowType)) pending[windowType] = source;
            else source.SetResult(prefabs[windowType]);

            return awaitable;
        }
    }

    private sealed class FakePresenter : IWindowPresenter
    {
        public void Tick() { }
        public void Dispose() { }
    }

    private GameObject root;
    private readonly List<GameObject> prefabObjects = new List<GameObject>();
    private FakeLoader loader;
    private InputBlocker inputBlocker;
    private WindowManager windows;
    private Dictionary<Type, UIWindow> views;
    private int presentersCreated;

    [SetUp]
    public void SetUp()
    {
        // UIRoot처럼 루트 Canvas 밑에 창마다 중첩 Canvas가 생긴다.
        root = new GameObject("UIRootForTest", typeof(RectTransform));
        root.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        loader = new FakeLoader();
        AddPrefab<TestPopupAWindow>();
        AddPrefab<TestPopupBWindow>();
        AddPrefab<TestPanelWindow>();
        AddPrefab<TestSystemWindow>();

        inputBlocker = new InputBlocker(root.transform);
        windows = new WindowManager(root.transform, loader, inputBlocker);
        views = new Dictionary<Type, UIWindow>();
        presentersCreated = 0;

        windows.Bind<TestPopupAWindow, TestPopupAArgs>((view, _, __) => Present(view));
        windows.Bind<TestPopupBWindow, TestPopupBArgs>((view, _, __) => Present(view));
        windows.Bind<TestPanelWindow, TestPanelArgs>((view, _, __) => Present(view));
        windows.Bind<TestSystemWindow, TestSystemArgs>((view, _, __) => Present(view));
    }

    [TearDown]
    public void TearDown()
    {
        windows.Dispose();
        Object.DestroyImmediate(root);
        foreach (GameObject prefab in prefabObjects) Object.DestroyImmediate(prefab);
        prefabObjects.Clear();
    }

    private void AddPrefab<TWindow>() where TWindow : UIWindow
    {
        var prefab = new GameObject(typeof(TWindow).Name, typeof(RectTransform));
        prefab.SetActive(false); // 프리팹처럼 꺼 둔다. WindowHost가 복제본을 켠다.
        loader.Add(prefab.AddComponent<TWindow>());
        prefabObjects.Add(prefab);
    }

    private IWindowPresenter Present(UIWindow view)
    {
        views[view.GetType()] = view;
        presentersCreated++;
        return new FakePresenter();
    }

    // 창의 Canvas(WindowHost가 붙인 것)
    private Canvas CanvasOf<TWindow>() where TWindow : UIWindow =>
        root.transform.Find(typeof(TWindow).Name).GetComponent<Canvas>();

    private int OrderOf<TWindow>() where TWindow : UIWindow => CanvasOf<TWindow>().sortingOrder;

    private Canvas Dim => root.transform.Find("Dim").GetComponent<Canvas>();

    private static bool IsClosed(WindowHandle handle) => handle.Closed.GetAwaiter().IsCompleted;

    private static WindowStatus StatusOf(WindowHandle handle)
    {
        var awaiter = handle.Closed.GetAwaiter();
        Assert.IsTrue(awaiter.IsCompleted, "창이 아직 닫히지 않았습니다.");
        return awaiter.GetResult();
    }

    // ── 구분 순서 삽입 ─────────────────────────────────────

    [Test]
    public void PopupOpenedAboveSystem_GoesBelowIt_AndBackDoesNotClosePopup()
    {
        WindowHandle system = windows.Open<TestSystemWindow>(new TestSystemArgs());
        WindowHandle popup = windows.Open<TestPopupAWindow>(new TestPopupAArgs());

        Assert.AreEqual(SystemBand, OrderOf<TestSystemWindow>());
        Assert.AreEqual(PopupBand, OrderOf<TestPopupAWindow>(), "구분 안의 깊이는 0부터(밑의 System 창과 무관)");

        Assert.IsTrue(windows.Back(), "맨 위(System 창)가 뒤로가기를 먹는다");
        Assert.IsFalse(IsClosed(popup), "열린 순서로는 맨 위인 Popup이 닫히면 안 된다");
        Assert.IsFalse(IsClosed(system));
    }

    [Test]
    public void SameBand_KeepsOpenOrder_AndDepthOrders()
    {
        WindowHandle panel = windows.Open<TestPanelWindow>(new TestPanelArgs());
        WindowHandle a = windows.Open<TestPopupAWindow>(new TestPopupAArgs());
        WindowHandle b = windows.Open<TestPopupBWindow>(new TestPopupBArgs());

        Assert.AreEqual(WindowBand, OrderOf<TestPanelWindow>());
        Assert.AreEqual(PopupBand + 0, OrderOf<TestPopupAWindow>());
        Assert.AreEqual(PopupBand + 10, OrderOf<TestPopupBWindow>(), "같은 구분은 열린 순, 창 하나가 10칸");

        windows.Back();
        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(b), "같은 구분에서는 나중에 연 창이 위");
        Assert.IsFalse(IsClosed(a));
        Assert.AreEqual(PopupBand + 0, OrderOf<TestPopupAWindow>());

        windows.Back();
        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(a));

        windows.Back();
        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(panel));
        Assert.IsFalse(windows.Back(), "창이 없으면 처리하지 않는다");
    }

    [Test]
    public void LowerBandOpenedLater_GoesBelowHigherBand()
    {
        WindowHandle a = windows.Open<TestPopupAWindow>(new TestPopupAArgs());
        WindowHandle panel = windows.Open<TestPanelWindow>(new TestPanelArgs());

        Assert.AreEqual(WindowBand, OrderOf<TestPanelWindow>());
        Assert.AreEqual(PopupBand, OrderOf<TestPopupAWindow>());

        windows.Back();
        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(a), "뒤로가기는 화면 맨 위(Popup)부터");
        Assert.IsFalse(IsClosed(panel));
    }

    // ── 로드 중인 창 ───────────────────────────────────────

    [Test]
    public void LoadingWindow_EatsBackOnTop_ThenMovesToItsBand()
    {
        WindowHandle b = windows.Open<TestPopupBWindow>(new TestPopupBArgs());

        loader.Defer<TestSystemWindow>();
        WindowHandle system = windows.Open<TestSystemWindow>(new TestSystemArgs());
        Assert.IsTrue(inputBlocker.IsBlocked.Value, "로드 중에는 입력을 막는다");

        Assert.IsTrue(windows.Back(), "로드 중인 창이 맨 위에서 뒤로가기를 먹는다");
        Assert.IsFalse(IsClosed(b), "로드 중인 창 밑의 Popup이 닫히면 안 된다");

        loader.Finish<TestSystemWindow>();
        Assert.IsFalse(inputBlocker.IsBlocked.Value);
        Assert.AreEqual(SystemBand, OrderOf<TestSystemWindow>());

        // System 창이 맨 위인 채로, 늦게 로드된 Popup은 같은 구분의 맨 위(B 위)로 간다.
        loader.Defer<TestPopupAWindow>();
        WindowHandle a = windows.Open<TestPopupAWindow>(new TestPopupAArgs());
        loader.Finish<TestPopupAWindow>();

        Assert.AreEqual(PopupBand + 0, OrderOf<TestPopupBWindow>());
        Assert.AreEqual(PopupBand + 10, OrderOf<TestPopupAWindow>());
        Assert.AreEqual(SystemBand, OrderOf<TestSystemWindow>());

        windows.Back();
        Assert.IsFalse(IsClosed(a) || IsClosed(b) || IsClosed(system), "맨 위는 여전히 뒤로가기로 닫히지 않는 System 창");
    }

    [Test]
    public void LoadsFinishingOutOfOrder_StackFollowsScreen()
    {
        loader.Defer<TestPopupAWindow>();
        loader.Defer<TestPopupBWindow>();
        WindowHandle a = windows.Open<TestPopupAWindow>(new TestPopupAArgs());
        WindowHandle b = windows.Open<TestPopupBWindow>(new TestPopupBArgs());

        loader.Finish<TestPopupBWindow>();
        Assert.AreEqual(PopupBand, OrderOf<TestPopupBWindow>());
        Assert.IsTrue(windows.Back(), "아직 로드 중인 A가 맨 위라 먹기만 한다");
        Assert.IsFalse(IsClosed(b));

        loader.Finish<TestPopupAWindow>();
        Assert.AreEqual(PopupBand + 10, OrderOf<TestPopupAWindow>(), "먼저 열린(보인) B 위");

        windows.Back();
        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(a), "화면 맨 위가 먼저 닫힌다");
        Assert.IsFalse(IsClosed(b));
    }

    // ── 닫기 기준(Exclusive, 탭 전환) ──────────────────────

    [Test]
    public void CloseBelowSystem_ClosesLowerBands_KeepsSystemWindow()
    {
        WindowHandle panel = windows.Open<TestPanelWindow>(new TestPanelArgs());
        WindowHandle a = windows.Open<TestPopupAWindow>(new TestPopupAArgs());
        WindowHandle system = windows.Open<TestSystemWindow>(new TestSystemArgs());

        windows.CloseBelow(UISortBand.System);

        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(panel));
        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(a));
        Assert.IsFalse(IsClosed(system));
        Assert.AreEqual(SystemBand, OrderOf<TestSystemWindow>());
        Assert.AreEqual(SystemBand - 1, Dim.sortingOrder);
    }

    [Test]
    public void CloseBelow_WhileLoading_AppliesWhenLoadFinishes()
    {
        loader.Defer<TestPopupAWindow>();
        loader.Defer<TestSystemWindow>();
        WindowHandle a = windows.Open<TestPopupAWindow>(new TestPopupAArgs());
        WindowHandle system = windows.Open<TestSystemWindow>(new TestSystemArgs());

        windows.CloseBelow(UISortBand.System);
        Assert.IsFalse(IsClosed(a) || IsClosed(system), "구분을 모르는 동안에는 닫지 않는다");

        // 닫기 기준 뒤에 요청한 창에는 적용되지 않는다.
        loader.Defer<TestPopupBWindow>();
        WindowHandle b = windows.Open<TestPopupBWindow>(new TestPopupBArgs());

        loader.Finish<TestPopupAWindow>();
        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(a), "Popup은 기준(System)보다 낮아 로드가 끝나자마자 닫힌다");
        Assert.IsFalse(views.ContainsKey(typeof(TestPopupAWindow)), "닫힌 창의 Presenter는 만들지 않는다");
        Assert.IsFalse(CanvasOf<TestPopupAWindow>().enabled, "늦게 온 창은 숨긴 채로 둔다");

        loader.Finish<TestSystemWindow>();
        Assert.IsFalse(IsClosed(system), "System 창은 기준보다 낮지 않아 남는다");

        loader.Finish<TestPopupBWindow>();
        Assert.IsFalse(IsClosed(b), "기준을 받은 뒤에 연 창은 닫지 않는다");

        Assert.AreEqual(2, presentersCreated);
        Assert.IsFalse(inputBlocker.IsBlocked.Value, "닫힌 창의 입력 차단도 풀린다");
        Assert.AreEqual(SystemBand, OrderOf<TestSystemWindow>());
        Assert.AreEqual(PopupBand, OrderOf<TestPopupBWindow>());
    }

    [Test]
    public void CloseBelow_KeepsHighestThresholdReceivedWhileLoading()
    {
        loader.Defer<TestPanelWindow>();
        WindowHandle panel = windows.Open<TestPanelWindow>(new TestPanelArgs());

        windows.CloseBelow(UISortBand.System);
        windows.CloseBelow(UISortBand.Fx); // 더 낮은 기준이 와도 높은 기준이 남는다

        loader.Finish<TestPanelWindow>();
        Assert.AreEqual(WindowStatus.Dismissed, StatusOf(panel));
    }

    // ── 닫히지 않는 창 ─────────────────────────────────────

    [Test]
    public void WindowThatRefusesBackAndOutside_StaysOpen()
    {
        WindowHandle system = windows.Open<TestSystemWindow>(new TestSystemArgs());

        for (int i = 0; i < 3; i++) Assert.IsTrue(windows.Back(), "뒤로가기는 먹는다(밑의 UI로 넘어가지 않는다)");
        Dim.GetComponentInChildren<Button>().onClick.Invoke(); // 딤(바깥) 터치

        Assert.IsFalse(IsClosed(system));
        Assert.IsTrue(windows.HasModal.Value);
    }

    // ── 딤 ─────────────────────────────────────────────────

    [Test]
    public void Dim_SitsJustBelowTopModal()
    {
        Assert.IsFalse(Dim.enabled, "모달이 없으면 딤도 없다");

        windows.Open<TestPanelWindow>(new TestPanelArgs());
        Assert.IsFalse(Dim.enabled, "모달이 아닌 창은 딤을 띄우지 않는다");

        WindowHandle a = windows.Open<TestPopupAWindow>(new TestPopupAArgs());
        Assert.IsTrue(Dim.enabled);
        Assert.AreEqual(PopupBand - 1, Dim.sortingOrder);

        WindowHandle system = windows.Open<TestSystemWindow>(new TestSystemArgs());
        Assert.AreEqual(SystemBand - 1, Dim.sortingOrder, "딤은 맨 위 모달(System) 바로 밑, Popup은 딤 아래");

        windows.Open<TestPopupBWindow>(new TestPopupBArgs()); // System 밑에 들어간다
        Assert.AreEqual(SystemBand - 1, Dim.sortingOrder);

        system.Close();
        Assert.AreEqual(PopupBand + 10 - 1, Dim.sortingOrder, "System이 닫히면 남은 맨 위 모달(B) 밑으로");

        a.Close();
        Assert.AreEqual(PopupBand - 1, Dim.sortingOrder, "B가 깊이 0으로 내려온다");
    }
}
