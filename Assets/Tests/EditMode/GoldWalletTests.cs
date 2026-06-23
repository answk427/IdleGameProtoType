using NUnit.Framework;

[TestFixture]
public class GoldWalletTests
{
    [Test]
    public void NewWallet_StartsAtZero()
    {
        var wallet = new GoldWallet();
        Assert.AreEqual(0, wallet.Gold);
    }

    [Test]
    public void AddGold_IncreasesBalance_AndRaisesEvent()
    {
        var wallet = new GoldWallet();
        int? raised = null;
        wallet.OnGoldChanged += g => raised = g;

        wallet.AddGold(100);

        Assert.AreEqual(100, wallet.Gold);
        Assert.AreEqual(100, raised);
    }

    [Test]
    public void TrySpendGold_WithEnoughBalance_DeductsAndReturnsTrue()
    {
        var wallet = new GoldWallet();
        wallet.AddGold(100);

        bool success = wallet.TrySpendGold(40);

        Assert.IsTrue(success);
        Assert.AreEqual(60, wallet.Gold);
    }

    [Test]
    public void TrySpendGold_WithInsufficientBalance_FailsAndDoesNotChangeBalance()
    {
        var wallet = new GoldWallet();
        wallet.AddGold(10);

        bool success = wallet.TrySpendGold(50);

        Assert.IsFalse(success);
        Assert.AreEqual(10, wallet.Gold);
    }

    [Test]
    public void TrySpendGold_WithZeroOrNegativeAmount_AlwaysSucceedsWithoutChange()
    {
        var wallet = new GoldWallet();
        wallet.AddGold(10);

        Assert.IsTrue(wallet.TrySpendGold(0));
        Assert.IsTrue(wallet.TrySpendGold(-5));
        Assert.AreEqual(10, wallet.Gold);
    }
}
