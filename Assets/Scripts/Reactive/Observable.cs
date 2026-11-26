using System;
using System.Collections.Generic;
using UnityEngine;

// 상태(ReactiveProperty)와 사건(Subject)의 공통 구독 모양: Subscribe(콜백) → IDisposable (docs/UI_DESIGN.md 5-2, 5-5).
//
// 알림 규약
// - 발행 시점의 구독자만 받는다. 알림 중에 구독/해제해도 순회가 깨지지 않고, 해제된 구독자는 즉시 호출에서 빠진다.
// - 알림 중 다시 발행하면 지금 알림이 끝난 뒤 이어서 알린다. 모든 구독자가 같은 순서로 값을 받는다.
//   끝나지 않는 연쇄는 개발 빌드에서 "알림 한 번이 이어서 일으킨 전달 수"로 끊는다.
//   깊이로 세면 안 된다: 구독자 둘이 각각 값을 바꾸면 알림이 갑절로 늘어 얕은 깊이에서 이미 멈춘다.
// - 한 구독자의 예외가 다른 구독자의 알림을 막지 않는다.
//
// 메인 스레드 전용이다.
public abstract class Observable<T> : IChangeSignal
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private const int MaxFollowUpDeliveries = 1000;
#endif

    // 바뀔 때마다 새 배열로 교체한다(copy-on-write). 발행할 때는 참조만 잡으면 복사본이 되므로,
    // 자주 일어나는 발행에서 할당이 없고 드문 구독/해제에서만 할당한다.
    private Subscription[] subscriptions = Array.Empty<Subscription>();

    private readonly Queue<Notification> pending = new Queue<Notification>();
    private bool isDispatching;

    public IDisposable Subscribe(Action<T> onNext)
    {
        if (onNext == null) throw new ArgumentNullException(nameof(onNext));

        var subscription = new Subscription(this, onNext);

        var next = new Subscription[subscriptions.Length + 1];
        Array.Copy(subscriptions, next, subscriptions.Length);
        next[subscriptions.Length] = subscription;
        subscriptions = next;

        if (TryGetCurrent(out T current))
        {
            // 알림 중에 구독했으면 대기 중인 알림의 대상이 아니므로, 여기서 최신 값을 바로 준다.
            if (isDispatching) subscription.Invoke(current);
            else Dispatch(new Notification(current, new[] { subscription }));
        }

        return subscription;
    }

    // 값은 보지 않고 "바뀌었다(일어났다)"만 받는다. 파생 상태의 원천 신호로 쓴다(DerivedProperty).
    // 상태는 구독하는 순간 한 번 신호가 온다(지금 값 전달과 같다).
    public IDisposable SubscribeChange(Action onChange)
    {
        if (onChange == null) throw new ArgumentNullException(nameof(onChange));
        return Subscribe(_ => onChange());
    }

    // 구독하는 순간 건네줄 현재 값. 사건에는 현재 값이라는 개념이 없다.
    protected virtual bool TryGetCurrent(out T current)
    {
        current = default;
        return false;
    }

    protected void Publish(T value)
    {
        var notification = new Notification(value, subscriptions);

        if (isDispatching) pending.Enqueue(notification);
        else Dispatch(notification);
    }

    private void Dispatch(Notification first)
    {
        isDispatching = true;
        try
        {
            Deliver(first);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int followUps = 0;
#endif
            while (pending.Count > 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (++followUps > MaxFollowUpDeliveries)
                {
                    Debug.LogError($"[{GetType().Name}<{typeof(T).Name}>] 알림 한 번이 {MaxFollowUpDeliveries}번 넘게 이어졌습니다. 콜백이 같은 값을 계속 바꾸는지 확인하세요.");
                    pending.Clear();
                    break;
                }
#endif
                Deliver(pending.Dequeue());
            }
        }
        finally
        {
            isDispatching = false;
        }
    }

    private void Deliver(Notification notification)
    {
        foreach (Subscription target in notification.Targets) target.Invoke(notification.Value);
    }

    private void Remove(Subscription subscription)
    {
        int index = Array.IndexOf(subscriptions, subscription);
        if (index < 0) return;

        var next = new Subscription[subscriptions.Length - 1];
        Array.Copy(subscriptions, 0, next, 0, index);
        Array.Copy(subscriptions, index + 1, next, index, next.Length - index);
        subscriptions = next;
    }

    private readonly struct Notification
    {
        public readonly T Value;
        public readonly Subscription[] Targets;

        public Notification(T value, Subscription[] targets)
        {
            Value = value;
            Targets = targets;
        }
    }

    private sealed class Subscription : IDisposable
    {
        private Observable<T> source;
        private Action<T> onNext;

        public Subscription(Observable<T> source, Action<T> onNext)
        {
            this.source = source;
            this.onNext = onNext;
        }

        public void Invoke(T value)
        {
            try
            {
                onNext?.Invoke(value);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void Dispose()
        {
            source?.Remove(this);
            // 손잡이가 남아 있어도 구독자(파괴된 View 등)를 붙잡지 않게 참조를 끊는다.
            source = null;
            onNext = null;
        }
    }
}
