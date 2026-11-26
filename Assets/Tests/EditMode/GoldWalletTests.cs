using System.Collections.Generic;
using NUnit.Framework;

// 골드 지갑: 보유량은 반응형 상태다(구독하는 순간 지금 값, 바뀔 때만 알림).
[TestFixture]
public class GoldWalletTests
{
    [Test]
    public void NewWallet_StartsAtZero()
    {
        var wallet = new GoldWallet();
        Assert.AreEqual(GameNumber.Zero, wallet.Gold.Value);
    }

    [Test]
    public void AddGold_IncreasesBalance_AndNotifies()
    {
        var wallet = new GoldWallet();
        var seen = new List<GameNumber>();
        wallet.Gold.Subscribe(seen.Add);

        wallet.AddGold(100);

        Assert.AreEqual((GameNumber)100, wallet.Gold.Value);
        CollectionAssert.AreEqual(new[] { GameNumber.Zero, (GameNumber)100 }, seen, "구독하는 순간 지금 값, 그 뒤 바뀐 값");
    }

    [Test]
    public void TrySpendGold_WithEnoughBalance_DeductsAndReturnsTrue()
    {
        var wallet = new GoldWallet();
        wallet.AddGold(100);

        bool success = wallet.TrySpendGold(40);

        Assert.IsTrue(success);
        Assert.AreEqual((GameNumber)60, wallet.Gold.Value);
    }

    [Test]
    public void TrySpendGold_WithInsufficientBalance_FailsWithoutChangeOrNotification()
    {
        var wallet = new GoldWallet();
        wallet.AddGold(10);
        int notified = 0;
        bool initial = true;
        wallet.Gold.Subscribe(_ => { if (!initial) notified++; });
        initial = false;

        bool success = wallet.TrySpendGold(50);

        Assert.IsFalse(success);
        Assert.AreEqual((GameNumber)10, wallet.Gold.Value);
        Assert.AreEqual(0, notified);
    }

    [Test]
    public void TrySpendGold_WithZeroOrNegativeAmount_AlwaysSucceedsWithoutChange()
    {
        var wallet = new GoldWallet();
        wallet.AddGold(10);

        Assert.IsTrue(wallet.TrySpendGold(0));
        Assert.IsTrue(wallet.TrySpendGold(-5));
        Assert.AreEqual((GameNumber)10, wallet.Gold.Value);
    }
}
