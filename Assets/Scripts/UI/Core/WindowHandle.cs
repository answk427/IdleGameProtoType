using System;
using System.Collections.Generic;
using UnityEngine;

public enum WindowStatus
{
    Closed,       // 창이 스스로 닫음 (닫기 버튼, 결과 반환)
    Dismissed,    // 밖에서 닫음 (뒤로가기, 바깥 터치, 탭 전환, handle.Close)
    AlreadyOpen,  // 같은 창이 이미 열려 있어 열지 않음
    Failed,       // 열기 실패 (로그 남음)
    Cancelled,    // UIRoot가 사라져 취소됨
}

public readonly struct WindowResult<T>
{
    public WindowStatus Status { get; }

    // Status가 Closed일 때만 의미가 있다.
    public T Value { get; }

    public WindowResult(WindowStatus status, T value)
    {
        Status = status;
        Value = value;
    }
}

// Open이 즉시 돌려주는 손잡이. 실패도 예외가 아니라 Closed의 결과로 온다.
public sealed class WindowHandle
{
    private readonly WindowSignal<WindowStatus> closed = new WindowSignal<WindowStatus>();

    internal Action RequestClose;

    internal WindowHandle() { }

    // 읽을 때마다 새 Awaitable을 준다. 꺼낸 것을 변수에 담아 두 번 await하지는 말 것.
    public Awaitable<WindowStatus> Closed => closed.Wait();

    public void Close() => RequestClose?.Invoke();

    internal void Complete(WindowStatus status)
    {
        RequestClose = null;
        closed.Set(status);
    }
}

public sealed class WindowHandle<T>
{
    private readonly WindowSignal<WindowResult<T>> closed = new WindowSignal<WindowResult<T>>();

    internal Action RequestClose;

    internal WindowHandle() { }

    public Awaitable<WindowResult<T>> Closed => closed.Wait();

    public void Close() => RequestClose?.Invoke();

    internal void Complete(WindowResult<T> result)
    {
        RequestClose = null;
        closed.Set(result);
    }
}

// 여러 번, 여러 곳에서 기다릴 수 있는 1회성 완료 신호.
// Awaitable은 풀에서 재사용돼 같은 인스턴스를 두 번 await하면 예외가 나므로, 기다리는 쪽마다 따로 만든다.
internal sealed class WindowSignal<T>
{
    private readonly List<AwaitableCompletionSource<T>> waiters = new List<AwaitableCompletionSource<T>>();
    private bool isSet;
    private T value;

    public Awaitable<T> Wait()
    {
        var source = new AwaitableCompletionSource<T>();
        Awaitable<T> awaitable = source.Awaitable;

        if (isSet) source.SetResult(value);
        else waiters.Add(source);

        return awaitable;
    }

    public void Set(T result)
    {
        if (isSet) return;
        isSet = true;
        value = result;

        // 이어지는 코드가 다시 기다릴 수 있으므로 복사본을 순회한다.
        AwaitableCompletionSource<T>[] current = waiters.ToArray();
        waiters.Clear();
        foreach (AwaitableCompletionSource<T> waiter in current) waiter.SetResult(value);
    }
}
