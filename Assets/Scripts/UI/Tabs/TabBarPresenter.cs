using System;
using System.Collections.Generic;
using UnityEngine;

// 하단 탭바의 로직. 탭 = Exclusive 창(WindowOptions.Tab, docs/UI_DESIGN.md 6-4).
// - 다른 탭을 누르면 그 탭 창을 연다. 이전 탭과 그 위의 창은 WindowManager가 닫는다(Exclusive).
// - 같은 탭을 다시 누르면 화면을 비운다(System 구분 밑을 모두 닫는다). 탭 전환과 같은 "화면 이동"이다.
// - 열 수 있는 탭은 UIRoot가 여는 함수를 넘긴 탭뿐이고 나머지는 잠긴다(지금 잠긴 탭은 모두 내용 창이 없다).
//   탭 창을 여는 함수는 이 Presenter만 받으므로 잠금을 우회해 탭을 여는 길이 없다.
//   게임 규칙에 따른 해금(스테이지 클리어 등)은 2차 "해금"에서 게임 쪽 상태로 만들어 여기서 구독한다(5-8).
// - 지금 탭 = 이 Presenter가 열었고 아직 닫히지 않은 탭 창 중 마지막 것. 뒤로가기·로드 실패·밖에서 닫은 경우도
//   핸들의 닫힘으로 따라온다. 별도의 "지금 탭" 값을 손으로 맞추지 않는다.
public sealed class TabBarPresenter : IDisposable
{
    private sealed class OpenTab
    {
        public readonly UITabType Tab;
        public readonly WindowHandle Handle;

        public OpenTab(UITabType tab, WindowHandle handle)
        {
            Tab = tab;
            Handle = handle;
        }
    }

    private readonly ITabBarView view;
    private readonly IReadOnlyDictionary<UITabType, Func<WindowHandle>> openers;
    private readonly Action closeScreen;

    // 열기를 요청한 순서. 닫히면 빠진다.
    private readonly List<OpenTab> openTabs = new List<OpenTab>();
    private readonly ReactiveProperty<UITabType> current = new ReactiveProperty<UITabType>(UITabType.None);
    private readonly CompositeDisposable disposables = new CompositeDisposable();

    // 새 탭을 여는 동안(Exclusive가 이전 탭을 닫는 동안)은 지금 탭을 다시 계산하지 않는다.
    // 안 그러면 이전 탭이 닫힌 순간 새 탭의 핸들이 아직 없어서 "탭 없음"이 잠깐 지나간다.
    private bool isOpening;
    private bool isDisposed;

    public IReadOnlyReactiveProperty<UITabType> CurrentTab => current;

    // openers: 탭 → 그 탭의 창을 여는 함수. closeScreen: System 구분 밑의 창을 모두 닫는 함수.
    public TabBarPresenter(ITabBarView view, IReadOnlyDictionary<UITabType, Func<WindowHandle>> openers, Action closeScreen)
    {
        this.view = view;
        this.openers = openers;
        this.closeScreen = closeScreen;

        foreach (UITabType tab in view.Tabs) view.SetLocked(tab, !openers.ContainsKey(tab));

        current.Subscribe(view.SetSelected).AddTo(disposables);
        view.TabTapped += HandleTabTapped;
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        view.TabTapped -= HandleTabTapped;
        disposables.Dispose();
    }

    // 잠긴 탭을 누르면 아무 일도 없다(안내는 2차 ToastService).
    private void HandleTabTapped(UITabType tab)
    {
        if (isDisposed || !openers.TryGetValue(tab, out Func<WindowHandle> open)) return;

        if (tab == current.Value)
        {
            closeScreen();
            return;
        }

        WindowHandle handle;
        isOpening = true;
        try
        {
            handle = open();
        }
        finally
        {
            isOpening = false;
        }

        var openTab = new OpenTab(tab, handle);
        openTabs.Add(openTab);
        WatchClosed(openTab); // 이미 닫힌 핸들(열기 실패)이면 여기서 바로 빠진다
        Refresh();
    }

    private async void WatchClosed(OpenTab openTab)
    {
        try
        {
            await openTab.Handle.Closed;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        openTabs.Remove(openTab);
        if (!isOpening) Refresh();
    }

    private void Refresh()
    {
        if (isDisposed) return;
        current.Value = openTabs.Count > 0 ? openTabs[openTabs.Count - 1].Tab : UITabType.None;
    }
}
