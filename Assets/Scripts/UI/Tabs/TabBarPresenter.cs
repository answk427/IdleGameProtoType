using System;
using System.Collections.Generic;
using UnityEngine;

// 탭 하나에 UIRoot가 넘기는 것. 내용 창이 없는 탭은 표에 넣지 않는다(잠김, "준비 중").
public sealed class TabEntry
{
    // 그 탭의 창을 연다. 탭 창을 여는 길은 이것뿐이다(잠금 우회 불가).
    public Func<WindowHandle> Open { get; }

    // 게임 쪽 해금 상태(GameSession.Unlocks)와 그 조건(안내 문장용. 누를 때 읽는다: 튜닝 덮어쓰기로 값이 바뀌어도 맞게).
    public IReadOnlyReactiveProperty<bool> Unlocked { get; }
    public Func<UnlockCondition> Condition { get; }

    // 레드닷 조건(게임 쪽 파생 상태를 UIRoot가 묶은 것). null이면 점이 없다.
    public IReadOnlyReactiveProperty<bool> Badge { get; }

    public TabEntry(Func<WindowHandle> open, IReadOnlyReactiveProperty<bool> unlocked, Func<UnlockCondition> condition,
        IReadOnlyReactiveProperty<bool> badge = null)
    {
        Open = open ?? throw new ArgumentNullException(nameof(open));
        Unlocked = unlocked ?? throw new ArgumentNullException(nameof(unlocked));
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        Badge = badge;
    }
}

// 하단 탭바의 로직. 탭 = Exclusive 창(WindowOptions.Tab, docs/UI_DESIGN.md 6-4).
// - 다른 탭을 누르면 그 탭 창을 연다. 이전 탭과 그 위의 창은 WindowManager가 닫는다(Exclusive).
// - 같은 탭을 다시 누르면 화면을 비운다(System 구분 밑을 모두 닫는다). 탭 전환과 같은 "화면 이동"이다.
// - 열림 = 창이 연결됐고(UIRoot가 TabEntry를 넘김) 게임 쪽에서 해금됨(5-8, 10-6). 잠긴 탭을 누르면 이유를 토스트로 알린다
//   ("Lv.2부터 열립니다", 내용이 없으면 "준비 중입니다"). 지금 열린 탭이 잠기면(진행도가 내려감) 화면을 비운다.
// - 레드닷: 해금된 탭에만 그린다.
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

    // 내용 창이 없는 탭을 눌렀을 때.
    public const string NotReadyMessage = "준비 중입니다";

    private readonly ITabBarView view;
    private readonly IReadOnlyDictionary<UITabType, TabEntry> tabs;
    private readonly Action closeScreen;
    private readonly IToastService toasts;

    // 열기를 요청한 순서. 닫히면 빠진다.
    private readonly List<OpenTab> openTabs = new List<OpenTab>();
    private readonly ReactiveProperty<UITabType> current = new ReactiveProperty<UITabType>(UITabType.None);
    private readonly CompositeDisposable disposables = new CompositeDisposable();

    // 새 탭을 여는 동안(Exclusive가 이전 탭을 닫는 동안)은 지금 탭을 다시 계산하지 않는다.
    // 안 그러면 이전 탭이 닫힌 순간 새 탭의 핸들이 아직 없어서 "탭 없음"이 잠깐 지나간다.
    private bool isOpening;
    private bool isDisposed;

    public IReadOnlyReactiveProperty<UITabType> CurrentTab => current;

    // tabs: 내용 창이 있는 탭 → 여는 함수·해금·레드닷. closeScreen: System 구분 밑의 창을 모두 닫는 함수.
    public TabBarPresenter(ITabBarView view, IReadOnlyDictionary<UITabType, TabEntry> tabs, Action closeScreen,
        IToastService toasts)
    {
        this.view = view;
        this.tabs = tabs;
        this.closeScreen = closeScreen;
        this.toasts = toasts ?? throw new ArgumentNullException(nameof(toasts));

        foreach (UITabType tab in view.Tabs)
        {
            if (tabs.TryGetValue(tab, out TabEntry entry)) Watch(tab, entry);
            else
            {
                view.SetLocked(tab, true);
                view.SetBadge(tab, false);
            }
        }

        current.Subscribe(view.SetSelected).AddTo(disposables);
        view.TabTapped += HandleTabTapped;
    }

    // 잠긴 탭을 눌렀을 때의 안내. 숫자에 따라 조사가 바뀌지 않게 "부터"를 쓴다.
    public static string FormatLocked(UnlockCondition condition)
    {
        switch (condition.Type)
        {
            case UnlockConditionType.PlayerLevel: return $"Lv.{condition.Value}부터 열립니다";
            case UnlockConditionType.StageReached: return $"스테이지 {condition.Value}부터 열립니다";
            default: return NotReadyMessage; // 조건이 없는데 잠겨 있으면 데이터 오류(해금 표에 행이 없음, 로그 남음)
        }
    }

    private void Watch(UITabType tab, TabEntry entry)
    {
        void Render()
        {
            bool unlocked = entry.Unlocked.Value;
            view.SetLocked(tab, !unlocked);
            view.SetBadge(tab, unlocked && entry.Badge != null && entry.Badge.Value);
        }

        entry.Unlocked.Subscribe(unlocked =>
        {
            Render();
            if (!unlocked && current.Value == tab) closeScreen(); // 열려 있던 탭이 잠겼다
        }).AddTo(disposables);

        entry.Badge?.Subscribe(_ => Render()).AddTo(disposables);
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        view.TabTapped -= HandleTabTapped;
        disposables.Dispose();
    }

    // 잠긴 탭은 열지 않고 이유를 알린다.
    private void HandleTabTapped(UITabType tab)
    {
        if (isDisposed) return;
        if (!tabs.TryGetValue(tab, out TabEntry entry))
        {
            toasts.Show(NotReadyMessage);
            return;
        }
        if (!entry.Unlocked.Value)
        {
            toasts.Show(FormatLocked(entry.Condition()));
            return;
        }

        if (tab == current.Value)
        {
            closeScreen();
            return;
        }

        WindowHandle handle;
        isOpening = true;
        try
        {
            handle = entry.Open();
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
