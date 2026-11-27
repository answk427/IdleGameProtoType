using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using static CombatTestKit;

// 전투원 → 뷰 표(CombatantViews, docs/COMBAT_ARCHITECTURE.md 12절 D44). 전투원의 주인은 시뮬레이션이고,
// 뷰는 월드의 Joined에 붙고 Left에 떨어진다. 풀에서 다시 쓴 전투원은 같은 객체라, 같은 배달 안에서도 옛 뷰가 떨어지고 새 뷰가 붙어야 한다.
[TestFixture]
public class CombatantViewsTests
{
    private readonly List<GameObject> created = new List<GameObject>();
    private readonly Stack<CombatantHost> pool = new Stack<CombatantHost>();
    private int attached, detached;

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created) Object.DestroyImmediate(go);
        created.Clear();
        pool.Clear();
        attached = detached = 0;
    }

    private CombatantViews NewViews(CombatWorld world) => new CombatantViews(world.Events, Attach, Detach);

    // BattleView와 같은 규칙: 풀에서 꺼내(없으면 만들어) 붙이고, 떼면 풀로.
    private CombatantHost Attach(IReadOnlyCombatant combatant)
    {
        CombatantHost host = pool.Count > 0 ? pool.Pop() : NewHost();
        host.Bind(combatant);
        attached++;
        return host;
    }

    private void Detach(IReadOnlyCombatant combatant, CombatantHost host)
    {
        host.Unbind();
        pool.Push(host);
        detached++;
    }

    private CombatantHost NewHost()
    {
        var go = new GameObject("TestCombatantView");
        created.Add(go);
        return go.AddComponent<TestCombatantHost>();
    }

    [Test]
    public void Joined_AttachesView_LeftDetachesIt()
    {
        CombatWorld world = World();
        CombatantViews views = NewViews(world);
        Combatant c = Spawn(world, Team.Enemy, 1f, 0.5f, Stats(hp: 30));
        Deliver(world);

        Assert.IsTrue(views.TryGet(c, out CombatantHost host));
        Assert.AreSame(c, host.Combatant);
        Assert.AreEqual(30.0, host.Hp.Value.Max.ToDouble(), "Joined가 처음 HP를 그린다");

        world.Remove(c);
        Deliver(world);

        Assert.IsFalse(views.TryGet(c, out _));
        Assert.IsNull(host.Combatant);
        Assert.AreEqual(0, views.All.Count);
        Assert.AreEqual(1, detached);
    }

    // 같은 배달 안에서 같은 전투원이 빠졌다 다시 들어온다(EnemyRoster가 풀에서 다시 씀). 뷰는 하나만 남고, 새 생의 상태를 그린다.
    [Test]
    public void ReusedCombatant_InOneDelivery_OldViewLeavesThenNewViewJoins()
    {
        CombatWorld world = World();
        CombatantViews views = NewViews(world);
        Combatant c = Spawn(world, Team.Enemy, 1f, 0.5f, Stats(hp: 30));
        Deliver(world);

        world.Remove(c);
        c.Spawn(Spec(Stats(hp: 50), 0.5f), 2f, null);
        world.Add(c);
        Deliver(world);

        Assert.IsTrue(views.TryGet(c, out CombatantHost host));
        Assert.AreSame(c, host.Combatant);
        Assert.AreEqual(1, views.All.Count, "옛 뷰는 떨어졌고 하나만 붙어 있다");
        Assert.AreEqual(2, attached);
        Assert.AreEqual(1, detached);
        Assert.AreEqual(50.0, host.Hp.Value.Max.ToDouble(), "새 생의 HP");
    }

    // 떨어진 뷰를 다른 전투원이 다시 쓴다. 옛 전투원의 사건은 그 뷰에 가지 않는다.
    [Test]
    public void PooledView_ReusedByAnotherCombatant_OnlyGetsItsEvents()
    {
        CombatWorld world = World();
        CombatantViews views = NewViews(world);
        Combatant a = Spawn(world, Team.Enemy, 1f, 0.5f, Stats(hp: 30));
        Deliver(world);
        views.TryGet(a, out CombatantHost first);

        world.Remove(a);
        Combatant b = Spawn(world, Team.Enemy, 3f, 0.5f, Stats(hp: 80));
        Deliver(world);

        Assert.IsTrue(views.TryGet(b, out CombatantHost second));
        Assert.AreSame(first, second, "풀의 뷰를 다시 썼다");
        Assert.AreSame(b, second.Combatant);
        Assert.IsFalse(views.TryGet(a, out _));

        a.TakeDamage(GameNumber.FromDouble(5)); // a는 월드 밖이라 사건이 없다
        b.TakeDamage(GameNumber.FromDouble(5));
        Deliver(world);
        Assert.AreEqual(75.0, second.Hp.Value.Current.ToDouble());
    }
}
