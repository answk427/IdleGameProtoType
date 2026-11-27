// 사건: "방금 일어난 일". 현재 값이 없고 구독 이후 일어난 것만 받으며, 같은 값도 매번 알린다.
// 외부에는 Observable<T>로 노출한다.
public sealed class Subject<T> : Observable<T>
{
    public void OnNext(T value) => Publish(value);
}
