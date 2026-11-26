using NUnit.Framework;
using static CombatTestKit;

// 2단계 규칙(docs/COMBAT_ARCHITECTURE.md 8-4): 거리 하나(표면 거리), 대칭 대상 규칙, 단계 전이, 이동, 사망, 처리 순서.
[TestFixture]
public class CombatWorldTests
{
    // ── 거리와 대상 ──

    [Test]
    public void Gap_IsSurfaceDistance_AndNegativeWhenOverlapping()
    {
        CombatWorld world = World();
        Combatant a = Spawn(world, Team.Ally, 0f, 0.5f, Stats());
        Combatant b = Spawn(world, Team.Enemy, 3f, 1f, Stats());
        Combatant overlap = Spawn(world, Team.Enemy, 0.5f, 1f, Stats());

        Assert.AreEqual(1.5f, CombatWorld.Gap(a, b), 1e-6f);
        Assert.Less(CombatWorld.Gap(a, overlap), 0f);
    }

    [TestCase(1.0f, true)]   // 정확히 사거리
    [TestCase(0.999f, false)] // 바로 바깥
    public void Engagement_UsesSurfaceDistance_AtTheExactBoundary(float range, bool engages)
    {
        CombatWorld world = World();
        Combatant ally = Spawn(world, Team.Ally, 0f, 0.5f, Stats(interval: 1.0, range: range, policy: MovePolicy.Advance));
        Spawn(world, Team.Enemy, 2.5f, 1f, Stats()); // 표면 거리 1.0

        StepOnce(world);

        Assert.AreEqual(engages ? CombatantPhase.Engaged : CombatantPhase.Moving, ally.Phase);
    }

    [Test]
    public void Target_BehindIsAcquired_RuleIsSymmetric()
    {
        CombatWorld world = World();
        Combatant ally = Spawn(world, Team.Ally, 0f, 0.4f, Stats(interval: 1.0, range: 0.5f, policy: MovePolicy.Advance));
        Combatant behind = Spawn(world, Team.Enemy, -1f, 0.4f, Stats()); // 표면 거리 0.2, 뒤쪽

        StepOnce(world);

        Assert.AreEqual(CombatantPhase.Engaged, ally.Phase);
        Assert.AreSame(behind, ally.Target);
    }

    [Test]
    public void SameGap_PicksEarlierRegistered()
    {
        CombatWorld world = World();
        Combatant ally = Spawn(world, Team.Ally, 0f, 0.5f, Stats(interval: 1.0, range: 1f, policy: MovePolicy.Advance));
        Combatant first = Spawn(world, Team.Enemy, -1.5f, 0.5f, Stats());
        Spawn(world, Team.Enemy, 1.5f, 0.5f, Stats());

        StepOnce(world);

        Assert.AreSame(first, ally.Target);
    }

    [Test]
    public void ValidTarget_IsKept_EvenIfAnotherBecomesCloser()
    {
        CombatWorld world = World();
        Combatant ally = Spawn(world, Team.Ally, 0f, 0.5f, Stats(interval: 1.0, range: 2f, policy: MovePolicy.Advance));
        Combatant far = Spawn(world, Team.Enemy, 2.5f, 0.5f, Stats(hp: 1e9)); // 표면 1.5
        StepOnce(world);
        Assert.AreSame(far, ally.Target);

        Spawn(world, Team.Enemy, 1.2f, 0.5f, Stats(hp: 1e9)); // 표면 0.2
        Run(world, 60);

        Assert.AreSame(far, ally.Target, "유효한 대상은 바꾸지 않는다(떨림 방지)");
    }

    [Test]
    public void AoeSkill_RadiusIsSurfaceDistance_OnBothSides()
    {
        CombatWorld world = World();
        Skill aoe = TestSkills.Create(id: 2, cooldown: 100f, effectType: SkillEffectType.AoeDamage, castTime: 0.5f);
        // TestSkills의 value2(반경)는 0이라, 반경 0 = 몸이 닿아 있는(표면 거리 ≤ 0) 상대만 맞는다.
        var caster = new FakeHost();
        Combatant ally = Spawn(world, Team.Ally, 0f, 0.5f, Stats(range: 0f, policy: MovePolicy.Advance), skills: Loadout(aoe), host: caster);
        var left = new FakeHost();
        var right = new FakeHost();
        var apart = new FakeHost();
        Spawn(world, Team.Enemy, -1f, 0.5f, Stats(hp: 1e9), host: left);    // 표면 0(닿음)
        Spawn(world, Team.Enemy, 1f, 0.5f, Stats(hp: 1e9), host: right);    // 표면 0(닿음)
        Spawn(world, Team.Enemy, 1.001f, 0.5f, Stats(hp: 1e9), host: apart); // 표면 0.001

        Run(world, 60);

        Assert.AreEqual(1, caster.ActionsStarted, "시전은 한 번");
        Assert.AreEqual(2, caster.SkillHits, "타격 사건은 맞은 대상마다 하나");
        Assert.AreEqual(1, left.Damaged);
        Assert.AreEqual(1, right.Damaged);
        Assert.AreEqual(0, apart.Damaged);
    }

    // ── 능력은 데이터 ──

    [Test]
    public void WithoutAttackOrSkill_NeverEngages_EvenInContact()
    {
        CombatWorld world = World();
        Spawn(world, Team.Ally, 0f, 0.5f, Stats(hp: 1e9, policy: MovePolicy.Advance));
        Combatant slime = Spawn(world, Team.Enemy, 1f, 0.5f, Stats(interval: 0, range: 5f));

        Run(world, 100);

        Assert.AreEqual(CombatantPhase.Moving, slime.Phase);
    }

    [Test]
    public void AttackIntervalFromData_MakesAnEnemyAttack()
    {
        CombatWorld world = World();
        var allyHost = new FakeHost();
        Spawn(world, Team.Ally, 0f, 0.5f, Stats(hp: 1e9, policy: MovePolicy.Advance), host: allyHost);
        Combatant slime = Spawn(world, Team.Enemy, 1f, 0.5f, Stats(damage: 7, interval: 0.5, range: 0.1f));

        Run(world, 51); // 1.02초까지: 교전 다음 단계(0.02초)에 시작, 0.27초와 0.77초에 타격

        Assert.AreEqual(CombatantPhase.Engaged, slime.Phase);
        Assert.AreEqual(2, allyHost.Damaged);
        Assert.AreEqual((GameNumber)7, allyHost.LastDamage);
    }

    // ── 이동 ──

    [Test]
    public void Advance_MovesForwardOnlyWhileMoving_Hold_NeverMoves_Approach_UsesOwnSpeed()
    {
        // 3단계(9-3): 아군은 월드에서 실제로 전진하고, 제자리 몬스터는 움직이지 않으며, 보스는 자기 속도로 다가온다.
        CombatWorld world = World();
        Combatant ally = Spawn(world, Team.Ally, 0f, 0.5f, Stats(hp: 1e9, interval: 1.0, range: 0.1f, policy: MovePolicy.Advance, speed: 2f));
        Combatant hold = Spawn(world, Team.Enemy, 10f, 0.5f, Stats(policy: MovePolicy.Hold, speed: 5f));
        Combatant approach = Spawn(world, Team.Enemy, 20f, 0.5f, Stats(policy: MovePolicy.Approach, speed: 1f));

        StepOnce(world);
        Assert.AreEqual(2f * 0.02f, ally.X, 1e-6f, "이동 단계의 아군은 앞으로 간다");
        Assert.AreEqual(10f, hold.X, "제자리 몬스터는 움직이지 않는다(속도가 있어도)");
        Assert.AreEqual(20f - 1f * 0.02f, approach.X, 1e-5f, "접근 몬스터는 자기 속도로");

        // 교전하면 아군은 멈추고, 접근 몬스터는 계속 온다
        Spawn(world, Team.Enemy, 1.1f, 0.5f, Stats(hp: 1e9));
        StepOnce(world);
        Assert.AreEqual(CombatantPhase.Engaged, ally.Phase);
        float allyX = ally.X, approachX = approach.X;
        StepOnce(world);
        Assert.AreEqual(allyX, ally.X);
        Assert.Less(approach.X, approachX);
    }

    [TestCase(100f)]
    [TestCase(1e6f)]
    public void Approach_NeverPassesThroughTheOpponentBody_FromEitherSide(float speed)
    {
        CombatWorld world = World();
        Combatant ally = Spawn(world, Team.Ally, 0f, 0.4f, Stats(hp: 1e9, policy: MovePolicy.Advance, speed: 0f));
        Combatant right = Spawn(world, Team.Enemy, 50f, 1.5f, Stats(policy: MovePolicy.Approach, speed: speed));
        Combatant left = Spawn(world, Team.Enemy, -50f, 0.1f, Stats(policy: MovePolicy.Approach, speed: speed));

        Run(world, 30);

        Assert.AreEqual(ally.X + 0.4f + 1.5f, right.X, 1e-4f, "접촉선에서 멈춘다");
        Assert.AreEqual(ally.X - 0.4f - 0.1f, left.X, 1e-4f);
    }

    [TestCase(100f)]
    [TestCase(1e6f)]
    public void Advance_SweepStopsAtTheFirstLivingEnemyAhead_IgnoringCorpsesAndEnemiesBehind(float speed)
    {
        CombatWorld world = World();
        Combatant ally = Spawn(world, Team.Ally, 0f, 0.4f, Stats(hp: 1e9, policy: MovePolicy.Advance, speed: speed));
        Spawn(world, Team.Enemy, -3f, 0.5f, Stats(hp: 1e9));         // 뒤: 막지 않는다
        Combatant corpse = Spawn(world, Team.Enemy, 2f, 0.5f, Stats(hp: 1));
        corpse.TakeDamage(10);                                          // 시체: 막지 않는다
        Combatant wall = Spawn(world, Team.Enemy, 7f, 0.6f, Stats(hp: 1e9)); // 앞의 첫 살아 있는 적

        Run(world, 30);

        Assert.AreEqual(wall.X - 0.6f - 0.4f, ally.X, 1e-4f, "앞의 살아 있는 적 몸에서 멈춘다");
    }

    [Test]
    public void AlliesAreProcessedFirst_RegardlessOfRegistrationOrder()
    {
        // 적을 먼저 등록해도 아군이 먼저 움직이고, 적은 움직인 아군 몸을 기준으로 sweep한다.
        // (적이 먼저라면 아군 0.01, 적 1.01이 된다)
        CombatWorld world = World();
        Combatant enemy = Spawn(world, Team.Enemy, 1.03f, 0.5f, Stats(policy: MovePolicy.Approach, speed: 1f));
        Combatant ally = Spawn(world, Team.Ally, 0f, 0.5f, Stats(hp: 1e9, policy: MovePolicy.Advance, speed: 1f));

        StepOnce(world);

        Assert.AreEqual(0.02f, ally.X, 1e-6f);
        Assert.AreEqual(1.02f, enemy.X, 1e-6f);
    }

    // ── 원점 재설정(D16) ──

    [Test]
    public void ShiftOrigin_KeepsGapsTargetsPhases_AndRenderedPositionContinuous()
    {
        CombatWorld world = World();
        Combatant ally = Spawn(world, Team.Ally, 1000.3f, 0.4f, Stats(hp: 1e9, interval: 0.5, range: 0.5f, policy: MovePolicy.Advance, speed: 3f));
        Combatant enemy = Spawn(world, Team.Enemy, 1001.2f, 0.4f, Stats(hp: 1e9, policy: MovePolicy.Approach, speed: 1f));
        Run(world, 5);
        float gap = CombatWorld.Gap(ally, enemy);
        float renderBefore = ally.GetRenderX(0.5f);
        CombatantPhase phase = ally.Phase;
        Combatant target = ally.Target;
        float shifted = 0f;
        world.Events.Subscribe(e => { if (e.Type == CombatEventType.OriginShifted) shifted += e.Delta; });

        world.RecenterOn(ally);
        Deliver(world);

        Assert.AreEqual(1000f, shifted, "정수 부분만큼(뷰가 살아 있는 연출을 옮기는 사건)");
        Assert.AreEqual(1000.0, world.TotalShift);
        Assert.That(ally.X, Is.InRange(0f, 1f));
        Assert.AreEqual(gap, CombatWorld.Gap(ally, enemy), 1e-4f);
        Assert.AreEqual(renderBefore - 1000f, ally.GetRenderX(0.5f), 1e-3f, "그려지는 위치가 끊기지 않는다");
        Assert.AreEqual(phase, ally.Phase);
        Assert.AreSame(target, ally.Target);
    }

    [Test]
    public void RecenteringEveryEncounter_KeepsCoordinatesSmall_OverALongRun()
    {
        // 하루 방치(약 30만 유닛)에 해당하는 거리를 달려도 좌표가 작게 유지된다.
        CombatWorld world = World();
        Combatant ally = Spawn(world, Team.Ally, 0f, 0.4f, Stats(hp: 1e9, policy: MovePolicy.Advance, speed: 1000f));

        float maxAbsX = 0f;
        for (int encounter = 0; encounter < 20000; encounter++)
        {
            Run(world, 1); // 20유닛 전진
            world.RecenterOn(ally);
            maxAbsX = System.Math.Max(maxAbsX, System.Math.Abs(ally.X));
        }

        Assert.Less(maxAbsX, 1f);
        Assert.AreEqual(20000 * 20.0, world.TotalShift + ally.X, 1.0, "이동한 거리 = 누적 재설정 + 지금 좌표");
    }

    // 사건은 단계가 끝난 뒤 배달된다(5-A). 그래서 받는 쪽이 원점을 옮겨도 처리 도중이 아니다(예전에는 처리 도중의 알림이라 막아야 했다).
    // 그 재설정이 낸 사건은 이번 배달에 섞이지 않고 다음 배달로 간다.
    [Test]
    public void Listener_MayShiftOrigin_BecauseDeliveryIsAfterTheStep()
    {
        CombatWorld world = World();
        var host = new FakeHost();
        Spawn(world, Team.Ally, 0f, 0.5f, Stats(hp: 1e9, damage: 1000, interval: 0.1, range: 5f, policy: MovePolicy.Advance));
        Spawn(world, Team.Enemy, 1f, 0.5f, Stats(hp: 1), host: host);
        int shifted = 0;
        world.Events.Subscribe(e => { if (e.Type == CombatEventType.OriginShifted) shifted++; });
        host.OnDiedCallback = (_, __) => world.ShiftOrigin(1f);

        Assert.DoesNotThrow(() => Run(world, 10));
        Assert.AreEqual(1.0, world.TotalShift);
        Assert.AreEqual(1, shifted, "재설정 사건은 다음 배달에 온다");
    }

    [Test]
    public void Delivery_IsInOrder_WithEventTimes_AndOnlyAfterDeliver()
    {
        CombatWorld world = World();
        var host = new FakeHost();
        Spawn(world, Team.Ally, 0f, 0.5f, Stats(hp: 1e9, damage: 30, interval: 0.1, range: 5f, policy: MovePolicy.Advance));
        Combatant enemy = Spawn(world, Team.Enemy, 1f, 0.5f, Stats(hp: 100), host: host, reward: new CombatReward((GameNumber)12, 3));
        var times = new System.Collections.Generic.List<double>();
        var types = new System.Collections.Generic.List<CombatEventType>();
        world.Events.Subscribe(e => { if (e.Source == enemy) { times.Add(e.Time); types.Add(e.Type); } });

        enemy.TakeDamage(10);
        Assert.AreEqual(0, host.Damaged, "배달 전에는 오지 않는다");
        Assert.Greater(world.PendingEventCount, 0);
        Deliver(world);
        Assert.AreEqual(1, host.Damaged);

        Run(world, 30);

        CollectionAssert.IsOrdered(times, "일어난 시각 순서로 온다");
        Assert.AreEqual(1, host.Died);
        Assert.AreEqual(CombatEventType.Died, types[types.IndexOf(CombatEventType.Died)]);
        Assert.AreEqual((GameNumber)12, host.LastReward.Gold, "사망 사건이 스폰 때 정한 보상을 싣는다");
        Assert.AreEqual(3, host.LastReward.Exp);
        Assert.AreEqual(0, world.PendingEventCount);
    }

    [Test]
    public void BeforeJoiningAWorld_NothingIsEmitted_ThenJoinedAnnouncesIt()
    {
        CombatWorld world = World();
        var host = new FakeHost();
        var c = new Combatant(Team.Enemy);
        Attach(world, c, host);

        c.Spawn(Stats(hp: 50), 0f, 0.5f, Timelines(), null);
        c.TakeDamage(1); // 월드가 없다: 사건 없음
        world.Add(c);
        Deliver(world);

        Assert.AreEqual(1, host.Joined);
        Assert.AreEqual(0, host.Damaged);
    }

    // 배달 중에 생긴 사건(구독자가 피해를 주는 등)은 이번 순회에 섞이지 않고 다음 배달로 간다.
    [Test]
    public void EventsRaisedWhileDelivering_GoToTheNextDelivery()
    {
        CombatWorld world = World();
        var host = new FakeHost();
        Combatant enemy = Spawn(world, Team.Enemy, 1f, 0.5f, Stats(hp: 100), host: host);
        Deliver(world);
        bool hitBack = false;
        world.Events.Subscribe(e =>
        {
            if (e.Type == CombatEventType.Damaged && !hitBack) { hitBack = true; enemy.TakeDamage(1); }
        });

        enemy.TakeDamage(1);
        Deliver(world);
        Assert.AreEqual(1, host.Damaged);
        Assert.AreEqual(1, world.PendingEventCount);

        Deliver(world);
        Assert.AreEqual(2, host.Damaged);
    }

    // ── 사망 ──

    [Test]
    public void DeathWithTimeline_CompletesAfterClipLength_WithoutTimeline_Immediately()
    {
        CombatWorld world = World();
        var withHost = new FakeHost();
        var withoutHost = new FakeHost();
        Spawn(world, Team.Ally, 0f, 0.5f, Stats(hp: 1e9, damage: 1000, interval: 0.1, range: 5f, policy: MovePolicy.Advance));
        Spawn(world, Team.Enemy, 1f, 0.5f, Stats(hp: 1), timelines: Timelines(death: true), host: withHost);
        Spawn(world, Team.Enemy, 1.1f, 0.5f, Stats(hp: 1), timelines: Timelines(death: false), host: withoutHost);

        Run(world, 10); // 0.07초에 가까운 쪽, 0.17초에 다음 쪽이 죽는다
        Assert.AreEqual(1, withHost.Died);
        Assert.AreEqual(1, withoutHost.Died);
        Assert.AreEqual(1, withoutHost.DeathCompleted, "사망 시간표가 없으면 바로 끝");
        Assert.AreEqual(0, withHost.DeathCompleted, "사망 시간표(1초)가 끝나기 전");

        Run(world, 60);
        Assert.AreEqual(1, withHost.DeathCompleted);
    }

    [Test]
    public void RemovingDuringEvents_IsDeferred_AndDoesNotBreakTheStep()
    {
        CombatWorld world = World();
        var enemyHost = new FakeHost();
        Spawn(world, Team.Ally, 0f, 0.5f, Stats(hp: 1e9, damage: 1000, interval: 0.1, range: 5f, policy: MovePolicy.Advance));
        Combatant enemy = Spawn(world, Team.Enemy, 1f, 0.5f, Stats(hp: 1), host: enemyHost);
        Spawn(world, Team.Enemy, 1.5f, 0.5f, Stats(hp: 1e9));
        enemyHost.OnDiedCallback = (self, _) => world.Remove(self); // 사망 사건을 받은 쪽이 빼는 경우(배달은 처리 밖)

        Assert.DoesNotThrow(() => Run(world, 10));
        Assert.IsNull(enemy.World);
        Assert.AreEqual(2, world.Combatants.Count);
    }
}
