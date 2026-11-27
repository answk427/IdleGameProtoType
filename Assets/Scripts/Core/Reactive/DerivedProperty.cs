using System;
using System.Collections.Generic;

// 파생 상태: 다른 상태들로부터 계산되는 읽기 전용 상태(docs/UI_DESIGN.md 5-3, docs/COMBAT_ARCHITECTURE.md 10-10 D31).
// 예: 레벨업 가능 = 경험치 ≥ 필요 경험치, 전투 능력치 = 레벨 기본값 + 업그레이드.
//
// - 원천은 "바뀌었다"는 신호일 뿐이다. 계산은 주인이 가진 지금 값에서 한다(compute).
//   그래서 주인이 여러 값을 한꺼번에 바꿀 때(진행도 교체) 저장 데이터를 먼저 다 바꾸고 상태를 알리면, 파생 상태는 첫 신호에서
//   이미 최종 값이 되고 나머지 신호는 같은 값이라 무시된다. 중간 조합(새 레벨 + 옛 업그레이드)이 바깥에 보이지 않는다.
// - 값이 달라졌을 때만 알린다. 구독하는 순간 지금 값을 준다(상태와 같은 모양).
// - 발행에 할당이 없다. 구조체 값은 IEquatable<T>를 구현할 것(같은 값 판정에서 박싱 방지).
// - 주인이 바깥 데이터가 바뀐 것(에디터의 튜닝 덮어쓰기)을 알면 Recompute를 부른다.
// R3로 옮긴다면 CombineLatest(...).ToReadOnlyReactiveProperty()에 해당한다(5-5).
public sealed class DerivedProperty<T> : Observable<T>, IReadOnlyReactiveProperty<T>, IDisposable
{
    private readonly Func<T> compute;
    private readonly IDisposable[] sourceSubscriptions;
    private T value;

    public DerivedProperty(Func<T> compute, params IChangeSignal[] sources)
    {
        this.compute = compute ?? throw new ArgumentNullException(nameof(compute));
        if (sources == null) throw new ArgumentNullException(nameof(sources));

        value = compute();
        sourceSubscriptions = new IDisposable[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null) throw new ArgumentNullException(nameof(sources), $"원천 {i}가 없습니다.");
            sourceSubscriptions[i] = sources[i].SubscribeChange(Recompute);
        }
    }

    public T Value => value;

    public void Recompute()
    {
        T next = compute();
        if (EqualityComparer<T>.Default.Equals(value, next)) return;

        value = next;
        Publish(next);
    }

    // 원천 구독을 끊는다. 이 뒤로는 바뀌지 않는다.
    public void Dispose()
    {
        for (int i = 0; i < sourceSubscriptions.Length; i++)
        {
            sourceSubscriptions[i]?.Dispose();
            sourceSubscriptions[i] = null;
        }
    }

    protected override bool TryGetCurrent(out T current)
    {
        current = value;
        return true;
    }
}
