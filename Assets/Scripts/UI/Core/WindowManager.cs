using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// 창 로드/열기/닫기, 스택, 정렬 순서, 뒤로가기, 열린 창 Tick (docs/UI_DESIGN.md 6장).
//
// - 스택(windows)은 닫는 순서, UISortBand는 그리는 순서. 깊이는 저장하지 않고 스택에서 매번 계산한다.
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

    // 인덱스 0이 바닥, 마지막이 맨 위. 로드 중인 창도 들어 있다(로드 중에 닫을 수 있어야 해서).
    private readonly List<Entry> windows = new List<Entry>();

    private readonly ReactiveProperty<bool> hasModal = new ReactiveProperty<bool>();

    private Entry topModal;
    private bool isDisposed;

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
        var entry = new Entry(this, typeof(TWindow), (status, _) => handle.Complete(status));
        handle.RequestClose = entry.Dismiss;

        Begin(entry, args);
        return handle;
    }

    public WindowHandle<TResult> Open<TWindow, TResult>(IWindowArgs<TWindow, TResult> args) where TWindow : UIWindow
    {
        var handle = new WindowHandle<TResult>();
        var entry = new Entry(this, typeof(TWindow), (status, value) =>
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

    // 위에서부터 전부 닫는다.
    public void CloseAll() => CloseEvery(WindowStatus.Dismissed);

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

            entry.Host = host;
            entry.Options = host.View.Options;

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
        public readonly Action<WindowStatus, object> Complete;
        public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();

        public WindowState State = WindowState.Opening;
        public WindowHost Host;
        public WindowOptions Options;
        public IWindowPresenter Presenter;
        public IDisposable InputLease;

        public Entry(WindowManager owner, Type windowType, Action<WindowStatus, object> complete)
        {
            this.owner = owner;
            WindowType = windowType;
            Complete = complete;
        }

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
