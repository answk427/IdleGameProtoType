using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// 확인창과 뒤로가기(docs/UI_DESIGN.md 6-3, 6-4, 10-6). 진짜 WindowManager + 가짜 로더.
// - DialogService: 창의 결과 다섯 가지 → "확인했는가" 하나. 확인만 true.
// - ConfirmDialogPresenter: 확인 = true, 취소 = false로 닫는다.
// - BackNavigator: 입력 차단 중 무시 → 맨 위 창 → 창이 없으면 종료 확인창. 확인창이 떠 있으면 뒤로가기가 그것을 닫는다.
[TestFixture]
public class DialogTests
{
    private sealed class FakePresenter : IWindowPresenter
    {
        public void Tick() { }
        public void Dispose() { }
    }

    private sealed class FakeConfirmView : IConfirmDialogView
    {
        public string Title, Message, Confirm, Cancel;

        public event Action ConfirmTapped;
        public event Action CancelTapped;

        public void SetContent(string title, string message, string confirmLabel, string cancelLabel)
        {
            Title = title;
            Message = message;
            Confirm = confirmLabel;
            Cancel = cancelLabel;
        }

        public void TapConfirm() => ConfirmTapped?.Invoke();
        public void TapCancel() => CancelTapped?.Invoke();
        public bool HasListeners => ConfirmTapped != null || CancelTapped != null;
    }

    private sealed class FakeContext : IWindowContext<bool>
    {
        public readonly List<bool> Results = new List<bool>();
        public void Close(bool result) => Results.Add(result);
    }

    private GameObject root;
    private readonly List<GameObject> prefabObjects = new List<GameObject>();
    private FakeWindowLoader loader;
    private WindowManager windows;
    private DialogService dialogs;
    private BackNavigator navigator;

    // 열린 확인창의 결과 통로(가짜 Presenter가 받는다). 열릴 때마다 바뀐다.
    private IWindowContext<bool> confirm;
    private ConfirmArgs lastArgs;
    private int confirmsOpened;
    private int quits;
    private bool blocked;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("UIRootForTest", typeof(RectTransform));
        root.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        loader = new FakeWindowLoader();
        AddPrefab<ConfirmDialogWindow>();
        AddPrefab<TestPopupAWindow>();
        AddPrefab<TestSystemWindow>();

        windows = new WindowManager(root.transform, loader, new InputBlocker(root.transform));
        windows.Bind<ConfirmDialogWindow, ConfirmArgs, bool>((_, args, context) =>
        {
            confirm = context;
            lastArgs = args;
            confirmsOpened++;
            return new FakePresenter();
        });
        windows.Bind<TestPopupAWindow, TestPopupAArgs>((_, __, ___) => new FakePresenter());
        windows.Bind<TestSystemWindow, TestSystemArgs>((_, __, ___) => new FakePresenter());

        dialogs = new DialogService(windows);
        confirm = null;
        lastArgs = null;
        confirmsOpened = 0;
        quits = 0;
        blocked = false;
        navigator = new BackNavigator(() => blocked, windows.Back, dialogs, () => quits++);
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
        prefab.SetActive(false);
        loader.Add(prefab.AddComponent<TWindow>());
        prefabObjects.Add(prefab);
    }

    private static bool IsDone(Awaitable<bool> answer) => answer.GetAwaiter().IsCompleted;

    private static bool AnswerOf(Awaitable<bool> answer)
    {
        var awaiter = answer.GetAwaiter();
        Assert.IsTrue(awaiter.IsCompleted, "확인창이 아직 닫히지 않았습니다.");
        return awaiter.GetResult();
    }

    // ── DialogService ──

    [Test]
    public void Confirm_TrueOnlyWhenConfirmed()
    {
        Awaitable<bool> yes = dialogs.Confirm(new ConfirmArgs("t", "m"));
        Assert.IsFalse(IsDone(yes), "닫히기 전에는 답이 없다");
        confirm.Close(true);
        Assert.IsTrue(AnswerOf(yes));

        Awaitable<bool> no = dialogs.Confirm(new ConfirmArgs("t", "m"));
        confirm.Close(false);
        Assert.IsFalse(AnswerOf(no));

        Awaitable<bool> back = dialogs.Confirm(new ConfirmArgs("t", "m"));
        Assert.IsTrue(windows.Back());
        Assert.IsFalse(AnswerOf(back), "뒤로가기(Dismissed)는 아니오");
    }

    [Test]
    public void Confirm_WhileAnotherIsOpen_IsNo_WithWarning()
    {
        Awaitable<bool> first = dialogs.Confirm(new ConfirmArgs("1", "m"));

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("이미 떠 있어"));
        Awaitable<bool> second = dialogs.Confirm(new ConfirmArgs("2", "m"));

        Assert.IsFalse(AnswerOf(second));
        Assert.IsFalse(IsDone(first), "먼저 뜬 확인창은 그대로");
        Assert.AreEqual("1", lastArgs.Title);
        confirm.Close(true);
        Assert.IsTrue(AnswerOf(first));
    }

    [Test]
    public void ToAnswer_MapsEveryStatus()
    {
        foreach (WindowStatus status in Enum.GetValues(typeof(WindowStatus)))
        {
            if (status == WindowStatus.AlreadyOpen) LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("이미 떠 있어"));

            bool expected = status == WindowStatus.Closed;
            Assert.AreEqual(expected, DialogService.ToAnswer(new WindowResult<bool>(status, true)), status.ToString());
        }

        Assert.IsFalse(DialogService.ToAnswer(new WindowResult<bool>(WindowStatus.Closed, false)));
    }

    [Test]
    public void Confirm_AfterWindowsDisposed_IsNo()
    {
        windows.Dispose();
        Assert.IsFalse(AnswerOf(dialogs.Confirm(new ConfirmArgs("t", "m"))));
    }

    // ── ConfirmDialogPresenter ──

    [Test]
    public void Presenter_ShowsTexts_DefaultsLabels_AndClosesWithAnswer()
    {
        var view = new FakeConfirmView();
        var context = new FakeContext();
        var presenter = new ConfirmDialogPresenter(view, new ConfirmArgs("제목", "본문"), context);

        Assert.AreEqual("제목", view.Title);
        Assert.AreEqual("본문", view.Message);
        Assert.AreEqual(ConfirmDialogPresenter.DefaultConfirmLabel, view.Confirm);
        Assert.AreEqual(ConfirmDialogPresenter.DefaultCancelLabel, view.Cancel);

        view.TapCancel();
        view.TapConfirm();
        CollectionAssert.AreEqual(new[] { false, true }, context.Results);

        presenter.Dispose();
        Assert.IsFalse(view.HasListeners);
    }

    [Test]
    public void Presenter_UsesGivenLabels()
    {
        var view = new FakeConfirmView();
        new ConfirmDialogPresenter(view, new ConfirmArgs("t", "m", "종료", "계속"), new FakeContext()).Dispose();

        Assert.AreEqual("종료", view.Confirm);
        Assert.AreEqual("계속", view.Cancel);
    }

    // ── BackNavigator ──

    [Test]
    public void EmptyStack_Back_AsksToQuit_AndQuitsOnlyWhenConfirmed()
    {
        navigator.HandleBack();
        Assert.AreEqual(1, confirmsOpened, "창이 없으면 종료 확인창");
        Assert.AreEqual(BackNavigator.QuitTitle, lastArgs.Title);
        Assert.AreEqual(BackNavigator.QuitConfirmLabel, lastArgs.ConfirmLabel);

        confirm.Close(false);
        Assert.AreEqual(0, quits, "취소하면 종료하지 않는다");

        navigator.HandleBack();
        confirm.Close(true);
        Assert.AreEqual(1, quits);
    }

    [Test]
    public void Back_WhileQuitConfirmIsOpen_ClosesIt_WithoutQuitting()
    {
        navigator.HandleBack();
        navigator.HandleBack();

        Assert.AreEqual(1, confirmsOpened, "두 번째 뒤로가기는 확인창을 닫는다(새로 열지 않는다)");
        Assert.AreEqual(0, quits);
        Assert.IsFalse(windows.Back(), "확인창이 닫혀 스택이 비었다");

        navigator.HandleBack();
        Assert.AreEqual(2, confirmsOpened, "다시 누르면 다시 묻는다");
    }

    // 로드가 늦어도(Addressables) 연타로 확인창이 둘 뜨지 않는다: 여는 중인 창이 뒤로가기를 먹는다.
    [Test]
    public void BackSpam_WhileQuitConfirmIsLoading_OpensOne()
    {
        loader.Defer<ConfirmDialogWindow>();
        navigator.HandleBack();
        navigator.HandleBack();
        navigator.HandleBack();

        loader.Finish<ConfirmDialogWindow>();
        Assert.AreEqual(1, confirmsOpened);
        confirm.Close(true);
        Assert.AreEqual(1, quits);
    }

    [Test]
    public void Back_WithAWindowOpen_ClosesTheWindow_NotAskingToQuit()
    {
        WindowHandle popup = windows.Open<TestPopupAWindow>(new TestPopupAArgs());

        navigator.HandleBack();

        Assert.IsTrue(popup.Closed.GetAwaiter().IsCompleted);
        Assert.AreEqual(0, confirmsOpened);
    }

    // 세이브 오류 창처럼 뒤로가기로 닫히지 않는 System 창이 있으면 그 창이 먹는다. 종료 확인창이 그 위로 뜨지 않는다.
    [Test]
    public void Back_UnderNonClosableSystemWindow_IsEaten()
    {
        windows.Open<TestSystemWindow>(new TestSystemArgs());

        navigator.HandleBack();
        navigator.HandleBack();

        Assert.AreEqual(0, confirmsOpened);
        Assert.AreEqual(0, quits);
    }

    [Test]
    public void Back_WhileInputBlocked_DoesNothing()
    {
        blocked = true;
        navigator.HandleBack();
        Assert.AreEqual(0, confirmsOpened);

        WindowHandle popup = windows.Open<TestPopupAWindow>(new TestPopupAArgs());
        navigator.HandleBack();
        Assert.IsFalse(popup.Closed.GetAwaiter().IsCompleted, "차단 중에는 창도 닫지 않는다");
    }
}
