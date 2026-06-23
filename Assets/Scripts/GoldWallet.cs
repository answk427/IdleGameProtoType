using System;

// 골드(재화) 보유량과 증감 로직만 담당하는 순수 클래스.
// GameManager에서 분리됨 - MonoBehaviour가 아니라 PlayerStats와 같은 패턴으로,
// 단위 테스트와 재사용이 쉽도록 의도적으로 Unity 라이프사이클에 묶지 않았다.
public class GoldWallet
{
    public event Action<int> OnGoldChanged;

    public int Gold { get; private set; }

    public void AddGold(int amount)
    {
        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    public bool TrySpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (Gold < amount) return false;

        Gold -= amount;
        OnGoldChanged?.Invoke(Gold);
        return true;
    }
}
