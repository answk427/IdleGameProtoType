using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// 창 로드/열기/닫기, 스택, 정렬 순서, 뒤로가기, 열린 창 Tick (docs/UI_DESIGN.md 6장).
//
// - 스택(windows)은 닫는 순서, UISortBand는 그리는 순서. 깊이는 저장하지 않고 스택에서 매번 계산한다.
// - 스택은 항상 [로드된 창: 구분 순, 같은 구분은 열린 순] + [로드 중인 창]이다(6-4). 그래서 스택 맨 위가 화면 맨 위다:
//   System 창 위로 Popup이 열려도 Popup은 그 밑에 들어가고, 뒤로가기는 System 창이 받는다.
//   창의 구분은 프리팹을 로드해야 알므로(옵션은 View의 가상 프로퍼티) 로드 중인 창은 맨 위에 두고(뒤로가기를 먹기만 한다),
//   로드가 끝나는 순간 자기 구분 자리로 옮긴다.
// - Exclusive 창(탭)은 열릴 때 System 구분 밑의 창을 닫는다(CloseBelow). 스택 방식도 구분처럼 로드해야 알므로
//   로드가 끝난 순간에 적용하고, 자기보다 먼저 열기를 요청한 창에만 적용한다. 그래서 결과가 요청할 때 알았던 것과 같다
//   (나중에 요청한 팝업은 새 탭 위에 남는다). 새 창의 로드가 실패하면 이전 창은 그대로다.
// - 요청 큐는 없다. 같은 창이 열려 있으면 AlreadyOpen, 로드 중 입력은 InputBlocker가 막는다.
// - 어떤 창이 있고 무엇이 필요한지는 모른다. 조립은 UIRoot가 Bind로 한다.
public sealed class WindowManager : IDisposable
{
    private readonly Transform root;
    private readonly IWindowLoader loader;
    private readonly InputBlocker inputBlocker;
    private readonly WindowDim dim;

    private readonly Dictionary<Type, Binding> bindings = new Dictionary<Type, Binding>();
    private readonly Dictionary<Type, WindowHost> hosts = new Dictionary<Type, WindowHost>();
    private readonly Dictionary<UISortBand, int> depths = new Dictionary<UISortBand, int>();

    // 인덱스 0이 바닥, 마지막이 맨 위. 로드 중인 창도 들어 있다(로드 중에 닫을 수 있어야 해서). 순서 규칙은 맨 위 주석.
    private readonly List<Entry> windows = new List<Entry>();

    private readonly ReactiveProperty<bool> hasModal = new ReactiveProperty<bool>();

    private Entry topModal;
    private bool isDisposed;

    // 열기 요청 순번. Exclusive가 "먼저 요청된 창"을 가리는 데 쓴다(스택 안의 위치는 구분 순이라 요청 순서가 아니다).
    private long nextSequence;

    public IReadOnlyReactiveProperty<bool> HasModal => hasModal;

    public WindowManager(Transform root, IWindowLoader loader, InputBlocker inputBlocker)
    {
        this.root = root;
        this.loader = loader;
        this.inputBlocker = inputBlocker;
        dim = new WindowDim(root, HandleOutsideClicked);
    }

    // ── 조립 ───────────────────────────────────────────────

    public void Bind<TWindow, TArgs>(Func<TWindow, TArgs, IWindowContext, IWindowPresenter> createPresenter)
        where TWindow : UIWindow<TArgs>
        where TArgs : IWindowArgs<TWindow>
    {
        bindings.Add(typeof(TWindow), new Binding(typeof(TArgs),
            (view, args, entry) => createPresenter((TWindow)view, (TArgs)args, entry)));
    }

    public void Bind<TWindow, TArgs, TResult>(Func<TWindow, TArgs, IWindowContext<TResult>, IWindowPresenter> createPresenter)
        where TWindow : UIWindow<TArgs, TResult>
        where TArgs : IWindowArgs<TWindow, TResult>
    {
        bindings.Add(typeof(TWindow), new Binding(typeof(TArgs),
            (view, args, entry) => createPresenter((TWindow)view, (TArgs)args, new ResultContext<TResult>(entry))));
    }

    // ── 열기 / 닫기 ─────────────────────────────────────────

    public WindowHandle Open<TWindow>(IWindowArgs<TWindow> args) where TWindow : UIWindow
    {
        var handle = new WindowHandle();
        var entry = new Entry(this, typeof(TWindow), nextSequence++, (status, _) => handle.Complete(status));
        handle.RequestClose = entry.Dismiss;

        Begin(entry, args);
        return handle;
    }

    public WindowHandle<TResult> Open<TWindow, TResult>(IWindowArgs<TWindow, TResult> args) where TWindow : UIWindow
    {
        var handle = new WindowHandle<TResult>();
        var entry = new Entry(this, typeof(TWindow), nextSequence++, (status, value) =>
            handle.Complete(new WindowResult<TResult>(status, value is TResult result ? result : default)));
        handle.RequestClose = entry.Dismiss;

        Begin(entry, args);
        return handle;
    }

    // 맨 위 창에게 먼저 기회를 주고, 처리하지 않으면 닫는다. 창이 하나도 없으면 false.
    public bool Back()
    {
        if (windows.Count == 0) return false;

        Entry top = windows[windows.Count - 1];
        if (top.State != WindowState.Open) return true; // 여는 중에는 먹기만 한다

        if (!top.Host.View.OnBack() && top.Options.CloseOnBack) Close(top, WindowStatus.Dismissed);
        return true;
    }

    // band보다 낮은 구분의 창을 위에서부터 닫는다(Dismissed). 탭 전환(Exclusive)은 System 밑의 창만 닫는다:
    // System 구분(치명 오류, 강제 안내)은 화면 이동과 상관없이 남아야 한다.
    // 로드 중인 창은 구분을 아직 모르므로 그 자리에서 닫지 않고 기준만 기억해 두었다가, 로드가 끝날 때 같은 규칙을 적용한다.
    // 그래서 결과가 구분을 처음부터 알았던 것과 같다(로드 중인 System 창은 남고, 로드 중인 Popup은 새 탭 위에 뜨지 않는다).
    public void CloseBelow(UISortBand band) => CloseBelow(band, long.MaxValue);

    // requestedBefore보다 먼저 열기를 요청한 창만 본다(Exclusive 창이 자기와 나중에 요청된 창을 건드리지 않게).
    private void CloseBelow(UISortBand band, long requestedBefore)
    {
        Entry[] current = windows.ToArray();
        for (int i = current.Length - 1; i >= 0; i--)
        {
            Entry entry = current[i];
            if (entry.Sequence >= requestedBefore || entry.State == WindowState.Closed) continue;

            if (!entry.IsLoaded) entry.RememberCloseBelow(band);
            else if (entry.Options.Band < band) Close(entry, WindowStatus.Dismissed);
        }
    }

    // 디버그 표시용 읽기 전용 사본(UIRoot.Describe → 개발자 도구).
    public void Describe(UIDebugSnapshot into)
    {
        foreach (Entry entry in windows)
        {
            bool loaded = entry.IsLoaded;
            into.Windows.Add(new UIDebugSnapshot.WindowInfo(
                entry.WindowType.Name,
                loaded,
                loaded ? entry.Options.Band : default,
                loaded && entry.Host.IsAlive ? entry.Host.Order : 0,
                entry.State == WindowState.Open,
                loaded && entry.Options.Modal));
        }

        into.DimOrder = topModal != null && topModal.Host != null && topModal.Host.IsAlive ? UISort.DimOrder(topModal.Host.Order) : (int?)null;
    }

    public void Tick()
    {
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            // Tick 안에서 창이 닫히면 목록이 줄어든다.
            if (i < windows.Count && windows[i].State == WindowState.Open) windows[i].Presenter?.Tick();
        }
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;
        CloseEvery(WindowStatus.Cancelled);
    }

    private void Begin(Entry entry, object args)
    {
        if (isDisposed)
        {
            Close(entry, WindowStatus.Cancelled);
            return;
        }

        if (args == null || !bindings.TryGetValue(entry.WindowType, out Binding binding) || binding.ArgsType != args.GetType())
        {
            Debug.LogError($"[WindowManager] {entry.WindowType.Name}을(를) 열 수 없습니다. UIRoot의 Bind와 인자 타입을 확인하세요.");
            Close(entry, WindowStatus.Failed);
            return;
        }

        foreach (Entry other in windows)
        {
            if (other.WindowType != entry.WindowType) continue;

            Close(entry, WindowStatus.AlreadyOpen);
            return;
        }

        windows.Add(entry);
        OpenAsync(entry, binding, args);
    }

    // 예외는 전부 여기서 결과(Failed)로 바꾸므로 async void로 시작해도 새지 않는다.
    private async void OpenAsync(Entry entry, Binding binding, object args)
    {
        try
        {
            // 로더가 취소를 무시해도 잠금이 남지 않도록 lease는 Entry가 들고 Close가 푼다.
            entry.InputLease = inputBlocker.Acquire($"WindowOpen:{entry.WindowType.Name}");

            if (!hosts.TryGetValue(entry.WindowType, out WindowHost host) || !host.IsAlive)
            {
                UIWindow prefab = await loader.LoadAsync(entry.WindowType, entry.Cancellation.Token);

                // 로드 중에 닫혔으면 늦게 온 프리팹은 쓰지 않는다.
                if (entry.State == WindowState.Closed) return;

                if (prefab == null)
                {
                    Debug.LogError($"[WindowManager] {entry.WindowType.Name} 프리팹이 없습니다. WindowRegistry를 확인하세요.");
                    Close(entry, WindowStatus.Failed);
                    return;
                }

                host = new WindowHost(root, prefab);
                hosts[entry.WindowType] = host;
            }

            WindowOptions options = host.View.Options;

            // 로드 중에 받은 닫기 기준을, 구분을 처음부터 알았던 것처럼 적용한다(CloseBelow).
            if (entry.ClosesOnLoad(options.Band))
            {
                host.Hide(); // 새로 만든 창은 보이는 채로 생긴다. 다시 쓰는 창은 이미 숨어 있다.
                Close(entry, WindowStatus.Dismissed);
                return;
            }

            // Exclusive: 먼저 요청된 창을 닫는다. 닫힌 창의 결과를 기다리던 쪽이 이 창을 닫았을 수도 있다.
            if (options.Stack == WindowStackMode.Exclusive)
            {
                CloseBelow(UISortBand.System, entry.Sequence);
                if (entry.State == WindowState.Closed)
                {
                    host.Hide();
                    return;
                }
            }

            entry.Host = host;
            entry.Options = options;
            PlaceByBand(entry);

            host.Show();
            Reorder();
            host.View.NotifyOpening();

            IWindowPresenter presenter = binding.CreatePresenter(host.View, args, entry);
            if (entry.State == WindowState.Closed)
            {
                // 만들어지는 중에 자기 창을 닫은 경우.
                presenter?.Dispose();
                return;
            }

            entry.Presenter = presenter;
            entry.State = WindowState.Open;
            if (entry.Options.Modal) UILayerFactory.ClearSelection();
            presenter?.Tick(); // 첫 프레임에 빈 값이 보이지 않게

            entry.ReleaseInput();
        }
        catch (Exception e)
        {
            // 이미 닫혔으면 취소로 인한 예외다.
            if (entry.State == WindowState.Closed && e is OperationCanceledException) return;

            Debug.LogException(e);
            Close(entry, WindowStatus.Failed);
        }
    }

    private void Close(Entry entry, WindowStatus status, object result = null)
    {
        if (entry.State == WindowState.Closed) return;
        entry.State = WindowState.Closed;

        windows.Remove(entry);
        entry.ReleaseInput();

        try
        {
            entry.Presenter?.Dispose();
            if (entry.Host != null && entry.Host.IsAlive) entry.Host.View.NotifyClosed();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        entry.Host?.Hide();
        entry.Cancellation.Cancel();
        Reorder();

        // 결과 통지는 정리가 다 끝난 뒤 맨 마지막. 기다리던 쪽이 곧바로 같은 창을 다시 열 수 있다.
        entry.Complete(status, result);
    }

    private void CloseEvery(WindowStatus status)
    {
        Entry[] current = windows.ToArray();
        for (int i = current.Length - 1; i >= 0; i--) Close(current[i], status);
    }

    private void HandleOutsideClicked()
    {
        if (topModal != null && topModal.State == WindowState.Open && topModal.Options.CloseOnOutside)
        {
            Close(topModal, WindowStatus.Dismissed);
        }
    }

    // ── 정렬 ───────────────────────────────────────────────

    // 로드가 끝난 창을 자기 구분 자리로 옮긴다: 같거나 낮은 구분의 로드된 창들 바로 위, 높은 구분과 로드 중인 창의 밑.
    private void PlaceByBand(Entry entry)
    {
        windows.Remove(entry);

        int index = 0;
        while (index < windows.Count && windows[index].IsLoaded && windows[index].Options.Band <= entry.Options.Band) index++;

        windows.Insert(index, entry);
    }

    // 깊이는 같은 구분 안에서 센다. 스택이 구분 순이라 아래에서부터 세면 된다.
    private void Reorder()
    {
        depths.Clear();
        topModal = null;

        foreach (Entry entry in windows)
        {
            if (entry.Host == null || !entry.Host.IsAlive) continue;

            UISortBand band = entry.Options.Band;
            depths.TryGetValue(band, out int depth);
            depths[band] = depth + 1;

            entry.Host.SetOrder(UISort.WindowOrder(band, depth));

            if (entry.Options.Modal && (topModal == null || entry.Host.Order >= topModal.Host.Order)) topModal = entry;
        }

        if (topModal != null) dim.Show(UISort.DimOrder(topModal.Host.Order), topModal.Host.View.DimColor);
        else dim.Hide();

        hasModal.Value = topModal != null;
    }

    // ── 내부 타입 ───────────────────────────────────────────

    private enum WindowState
    {
        Opening,
        Open,
        Closed,
    }

    private sealed class Binding
    {
        public readonly Type ArgsType;
        public readonly Func<UIWindow, object, Entry, IWindowPresenter> CreatePresenter;

        public Binding(Type argsType, Func<UIWindow, object, Entry, IWindowPresenter> createPresenter)
        {
            ArgsType = argsType;
            CreatePresenter = createPresenter;
        }
    }

    // 열기 한 번. 같은 창을 다시 열면 새 Entry라서, 지난 Presenter가 뒤늦게 Close를 불러도 새 창은 안 닫힌다.
    private sealed class Entry : IWindowContext
    {
        private readonly WindowManager owner;

        public readonly Type WindowType;
        public readonly long Sequence; // 열기 요청 순번
        public readonly Action<WindowStatus, object> Complete;
        public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();

        public WindowState State = WindowState.Opening;
        public WindowHost Host;
        public WindowOptions Options; // Host가 생긴 뒤에만(IsLoaded) 의미가 있다.
        public IWindowPresenter Presenter;
        public IDisposable InputLease;

        // 로드 중에 받은 가장 높은 닫기 기준(CloseBelow). 없으면 null.
        private UISortBand? closeBelowOnLoad;

        public Entry(WindowManager owner, Type windowType, long sequence, Action<WindowStatus, object> complete)
        {
            this.owner = owner;
            WindowType = windowType;
            Sequence = sequence;
            Complete = complete;
        }

        // 구분(옵션)을 안다. 로드가 끝나 자기 구분 자리에 놓인 뒤다.
        public bool IsLoaded => Host != null;

        public void RememberCloseBelow(UISortBand band)
        {
            if (closeBelowOnLoad == null || band > closeBelowOnLoad.Value) closeBelowOnLoad = band;
        }

        public bool ClosesOnLoad(UISortBand band) => closeBelowOnLoad != null && band < closeBelowOnLoad.Value;

        public void Close() => CloseWith(null);
        public void CloseWith(object result) => owner.Close(this, WindowStatus.Closed, result);
        public void Dismiss() => owner.Close(this, WindowStatus.Dismissed);

        public void ReleaseInput()
        {
            InputLease?.Dispose();
            InputLease = null;
        }
    }

    private sealed class ResultContext<TResult> : IWindowContext<TResult>
    {
        private readonly Entry entry;

        public ResultContext(Entry entry)
        {
            this.entry = entry;
        }

        public void Close(TResult result) => entry.CloseWith(result);
    }
}
