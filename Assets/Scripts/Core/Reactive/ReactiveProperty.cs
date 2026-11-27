using System.Collections.Generic;

// 상태: "지금 값"이 있는 것. 구독하는 순간 현재 값을 주고, 같은 값은 알리지 않는다.
// 같은 값 판정에 EqualityComparer를 쓰므로 구조체는 IEquatable<T>를 구현할 것(박싱 방지).
// 모음 데이터는 내용을 고치지 말고 새 목록으로 교체해야 알림이 간다.
public sealed class ReactiveProperty<T> : Observable<T>, IReadOnlyReactiveProperty<T>
{
    private T value;

    public ReactiveProperty(T initialValue = default)
    {
        value = initialValue;
    }

    public T Value
    {
        get => value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(this.value, value)) return;

            this.value = value;
            Publish(value);
        }
    }

    protected override bool TryGetCurrent(out T current)
    {
        current = value;
        return true;
    }
}
