using System;

// 값의 타입과 상관없이 "바뀌었다"만 알려 주는 모양. 파생 상태(DerivedProperty)가 여러 타입의 원천을 함께 받는 데 쓴다.
public interface IChangeSignal
{
    IDisposable SubscribeChange(Action onChange);
}
