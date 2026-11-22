using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// 창 타입 → 프리팹. "어떤 프리팹을 어떻게 가져오나"만 담당하고 창의 성격(옵션)은 모른다.
[CreateAssetMenu(fileName = "WindowRegistry", menuName = "Game/Window Registry")]
public class WindowRegistry : ScriptableObject, IWindowLoader
{
    [SerializeField] private List<UIWindow> prefabs = new List<UIWindow>();

    public Awaitable<UIWindow> LoadAsync(Type windowType, CancellationToken cancellationToken)
    {
        // 직접 참조라 바로 끝나지만, 로더가 바뀌어도 호출부가 그대로이도록 비동기 모양을 지킨다.
        var source = new AwaitableCompletionSource<UIWindow>();
        Awaitable<UIWindow> awaitable = source.Awaitable;
        source.SetResult(Find(windowType));
        return awaitable;
    }

    private UIWindow Find(Type windowType)
    {
        foreach (UIWindow prefab in prefabs)
        {
            if (prefab != null && prefab.GetType() == windowType) return prefab;
        }
        return null;
    }
}
