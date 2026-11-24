using System;

// 체력을 가진 대상(전투원 호스트). 체력바와 HUD가 구독한다. 값은 GameNumber(D5).
public interface IHasHp
{
    event Action<GameNumber, GameNumber> OnHpChanged; // (현재, 최대)
}
