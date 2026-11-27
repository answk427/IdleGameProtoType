// 골드(재화) 보유량과 증감 규칙만 담당하는 순수 클래스(GameSession이 갖는다).
// 골드는 상한 없이 지수적으로 커지므로 GameNumber로 다룬다. 보유량은 반응형 상태라 구독하는 순간 지금 값을 받고,
// 바뀌면 따라온다(docs/UI_DESIGN.md 5-3). 골드 획득 같은 연출용 사건은 쓰는 곳이 생길 때 만든다(D32).
public sealed class GoldWallet
{
    private readonly ReactiveProperty<GameNumber> gold = new ReactiveProperty<GameNumber>(GameNumber.Zero);

    public IReadOnlyReactiveProperty<GameNumber> Gold => gold;

    public void AddGold(GameNumber amount)
    {
        gold.Value = gold.Value + amount;
    }

    public bool TrySpendGold(GameNumber amount)
    {
        if (amount <= GameNumber.Zero) return true;
        if (gold.Value < amount) return false;

        gold.Value = gold.Value - amount;
        return true;
    }

    // 저장 데이터를 적용할 때 사용(GameSession.Apply). 구독하는 쪽은 그대로 따라온다.
    public void LoadGold(GameNumber amount)
    {
        gold.Value = amount;
    }
}
