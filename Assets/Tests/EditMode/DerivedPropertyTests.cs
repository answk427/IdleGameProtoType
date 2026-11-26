using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

// 파생 상태(DerivedProperty, docs/COMBAT_ARCHITECTURE.md 10-10 D31)의 규약:
// 구독하는 순간 지금 값, 원천이 바뀌면 다시 계산, 같은 값은 무시, 주인의 지금 값에서 계산(중간 조합이 안 보임), 해제, 할당 0.
[TestFixture]
public class DerivedPropertyTests
{
    // 주인 흉내: 값을 먼저 다 바꾸고 상태를 알린다.
    private sealed class Owner
    {
        public int A, B;
        public readonly ReactiveProperty<int> StateA = new ReactiveProperty<int>();
        public readonly ReactiveProperty<int> StateB = new ReactiveProperty<int>();

        public void SetBoth(int a, int b)
        {
            A = a;
            B = b;
            StateA.Value = a;
            StateB.Value = b;
        }
    }

    [Test]
    public void GivesCurrentValueOnSubscribe_AndRecomputesWhenSourceChanges()
    {
        var owner = new Owner();
        var sum = new DerivedProperty<int>(() => owner.A + owner.B, owner.StateA, owner.StateB);
        var seen = new List<int>();
        sum.Subscribe(seen.Add);

        owner.A = 2;
        owner.StateA.Value = 2;

        CollectionAssert.AreEqual(new[] { 0, 2 }, seen);
        Assert.AreEqual(2, sum.Value);
    }

    [Test]
    public void SameComputedValue_IsNotPublished()
    {
        var owner = new Owner();
        var isPositive = new DerivedProperty<bool>(() => owner.A > 0, owner.StateA);
        var seen = new List<bool>();
        isPositive.Subscribe(seen.Add);

        owner.A = 1; owner.StateA.Value = 1;
        owner.A = 5; owner.StateA.Value = 5; // 여전히 true

        CollectionAssert.AreEqual(new[] { false, true }, seen);
    }

    // 원천 값이 아니라 주인의 지금 값에서 계산하므로, 두 값을 한꺼번에 바꿀 때 중간 조합이 보이지 않는다.
    [Test]
    public void OwnerBatchChange_PublishesOnlyTheFinalValue()
    {
        var owner = new Owner();
        var product = new DerivedProperty<int>(() => owner.A * 10 + owner.B, owner.StateA, owner.StateB);
        var seen = new List<int>();
        bool initial = true;
        product.Subscribe(v => { if (!initial) seen.Add(v); });
        initial = false;

        owner.SetBoth(3, 4);

        CollectionAssert.AreEqual(new[] { 34 }, seen, "3*10+0(중간 조합)이 보이면 안 된다");
    }

    [Test]
    public void EventSource_TriggersRecompute()
    {
        int counter = 0;
        var ticked = new Subject<int>();
        var derived = new DerivedProperty<int>(() => counter, ticked);

        counter = 7;
        ticked.OnNext(0);

        Assert.AreEqual(7, derived.Value);
    }

    [Test]
    public void Recompute_PicksUpOutsideChanges()
    {
        int data = 1;
        var derived = new DerivedProperty<int>(() => data * 2);
        data = 5;

        derived.Recompute();

        Assert.AreEqual(10, derived.Value);
    }

    [Test]
    public void Dispose_StopsFollowingSources()
    {
        var owner = new Owner();
        var derived = new DerivedProperty<int>(() => owner.A, owner.StateA);
        derived.Dispose();

        owner.A = 9;
        owner.StateA.Value = 9;

        Assert.AreEqual(0, derived.Value);
    }

    [Test]
    public void SourceChange_ToDerivedSubscriber_AllocatesNothing()
    {
        var owner = new Owner();
        var sum = new DerivedProperty<int>(() => owner.A + owner.B, owner.StateA, owner.StateB);
        int last = 0;
        sum.Subscribe(v => last = v);
        owner.A = 1; owner.StateA.Value = 1; // 첫 실행의 일회성 할당을 빼고 잰다

        Assert.That(() =>
        {
            for (int i = 2; i < 200; i++)
            {
                owner.A = i;
                owner.StateA.Value = i;
            }
        }, Is.Not.AllocatingGCMemory());
        Assert.AreEqual(199, last);
    }
}
