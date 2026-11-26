using System;

// 상태를 외부에 노출할 때 쓰는 읽기 전용 모양. 값을 바꾸는 것은 상태의 주인만 한다.
// 바뀜 신호(IChangeSignal)도 주므로 바깥에서 받은 상태로 파생 상태를 만들 수 있다(예: 탭의 레드닷 = 여러 조건의 OR).
public interface IReadOnlyReactiveProperty<T> : IChangeSignal
{
    T Value { get; }
    IDisposable Subscribe(Action<T> onNext);
}
