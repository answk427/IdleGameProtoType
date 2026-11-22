using System;

// 상태를 외부에 노출할 때 쓰는 읽기 전용 모양. 값을 바꾸는 것은 상태의 주인만 한다.
public interface IReadOnlyReactiveProperty<T>
{
    T Value { get; }
    IDisposable Subscribe(Action<T> onNext);
}
