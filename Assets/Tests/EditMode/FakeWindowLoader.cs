using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// 창 테스트용 가짜 로더: 창마다 로드 완료를 늦추거나(Defer → Finish) 프리팹이 없는 것으로 끝낼 수 있다(FinishMissing).
// AwaitableCompletionSource의 완료는 그 자리에서 이어지므로 Finish를 부르면 WindowManager의 열기가 끝까지 진행된다.
public sealed class FakeWindowLoader : IWindowLoader
{
    private readonly Dictionary<Type, UIWindow> prefabs = new Dictionary<Type, UIWindow>();
    private readonly HashSet<Type> deferred = new HashSet<Type>();
    private readonly Dictionary<Type, AwaitableCompletionSource<UIWindow>> pending = new Dictionary<Type, AwaitableCompletionSource<UIWindow>>();

    public void Add(UIWindow prefab) => prefabs[prefab.GetType()] = prefab;

    // 다음 로드를 Finish까지 끝내지 않는다.
    public void Defer<TWindow>() where TWindow : UIWindow => deferred.Add(typeof(TWindow));

    public void Finish<TWindow>() where TWindow : UIWindow => Complete<TWindow>(prefabs[typeof(TWindow)]);

    // 레지스트리에 프리팹이 없을 때처럼 끝낸다(열기 실패).
    public void FinishMissing<TWindow>() where TWindow : UIWindow => Complete<TWindow>(null);

    public Awaitable<UIWindow> LoadAsync(Type windowType, CancellationToken cancellationToken)
    {
        var source = new AwaitableCompletionSource<UIWindow>();
        Awaitable<UIWindow> awaitable = source.Awaitable;

        if (deferred.Remove(windowType)) pending[windowType] = source;
        else source.SetResult(prefabs[windowType]);

        return awaitable;
    }

    private void Complete<TWindow>(UIWindow result)
    {
        AwaitableCompletionSource<UIWindow> source = pending[typeof(TWindow)];
        pending.Remove(typeof(TWindow));
        source.SetResult(result);
    }
}
