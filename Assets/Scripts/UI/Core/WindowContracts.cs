using System;
using System.Threading;
using UnityEngine;

// 인자 타입이 자기 창을 가리킨다. 다른 창의 인자를 Open에 넘기면 컴파일되지 않는다.
public interface IWindowArgs<TWindow> where TWindow : UIWindow { }

// 결과가 있는 창의 인자. Open(args)만 써도 결과 타입이 추론된다.
public interface IWindowArgs<TWindow, TResult> where TWindow : UIWindow { }

// 열 때마다 새로 만들고 닫힐 때 Dispose된다. 구독은 여기 묶는다.
public interface IWindowPresenter : IDisposable
{
    // WindowManager가 열린 창에만 매 프레임 호출한다. 매 프레임 값(쿨다운)만 여기서 폴링한다.
    void Tick();
}

// Presenter가 자기 창을 닫는 통로. WindowManager 전체를 넘기지 않는다.
public interface IWindowContext
{
    void Close();
}

public interface IWindowContext<TResult>
{
    void Close(TResult result);
}

// 창 프리팹을 어떻게 가져오는가. Addressables로 바꿔도 WindowManager는 그대로다.
public interface IWindowLoader
{
    // 없으면 null.
    Awaitable<UIWindow> LoadAsync(Type windowType, CancellationToken cancellationToken);
}
