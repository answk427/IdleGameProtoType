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

        Assert.AreEqual(1, caster.SkillHits);
        Assert.AreEqual(2, caster.LastSkillHitCount);
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
    public void Hold_MovesOnlyWhileAllyAdvances_Approach_MovesAlways()
    {
        CombatWorld world = World();
        Combatant ally = Spawn(world, Team.Ally, 0f, 0.5f, Stats(hp: 1e9, interval: 1.0, range: 0.1f, policy: MovePolicy.Advance, speed: 2f));
        Combatant hold = Spawn(world, Team.Enemy, 10f, 0.5f, Stats(policy: MovePolicy.Hold));
        Combatant approach = Spawn(world, Team.Enemy, 20f, 0.5f, Stats(policy: MovePolicy.Approach));

        StepOnce(world);
        Assert.AreEqual(10f - 2f * 0.02f, hold.X, 1e-5f, "아군이 전진 중이면 화면과 함께 다가온다");
        Assert.AreEqual(20f - 2f * 0.02f, approach.X, 1e-5f);

        // 아군이 교전하면(스크롤 멈춤) Hold는 멈추고 Approach는 계속 온다
        Spawn(world, Team.Enemy, 1.0f, 0.5f, Stats(hp: 1e9));
        StepOnce(world);
        Assert.AreEqual(CombatantPhase.Engaged, ally.Phase);
        float holdX = hold.X, approachX = approach.X;
        StepOnce(world);
        Assert.AreEqual(holdX, hold.X);
        Assert.Less(approach.X, approachX);
    }

    [TestCase(100f)]
    [TestCase(1e6f)]
    public void Ccd_NeverPassesThroughTheOpponentBody_FromEitherSide(float speed)
    {
        CombatWorld world = World();
        Combatant ally = Spawn(world, Team.Ally, 0f, 0.4f, Stats(hp: 1e9, policy: MovePolicy.Advance, speed: speed));
        Combatant right = Spawn(world, Team.Enemy, 50f, 1.5f, Stats(policy: MovePolicy.Approach));
        Combatant left = Spawn(world, Team.Enemy, -50f, 0.1f, Stats(policy: MovePolicy.Approach));

        Run(world, 30);

        Assert.AreEqual(ally.X + 0.4f + 1.5f, right.X, 1e-4f, "접촉선에서 멈춘다");
        Assert.AreEqual(ally.X - 0.4f - 0.1f, left.X, 1e-4f);
    }

    [Test]
    public void AlliesAreProcessedFirst_RegardlessOfRegistrationOrder()
    {
        // 적을 먼저 등록해도, 같은 단계에서 아군의 전진 여부가 적 이동에 쓰인다.
        CombatWorld world = World();
        Combatant hold = Spawn(world, Team.Enemy, 10f, 0.5f, Stats(policy: MovePolicy.Hold));
        Spawn(world, Team.Ally, 0f, 0.5f, Stats(hp: 1e9, policy: MovePolicy.Advance, speed: 1f));

        StepOnce(world);

        Assert.IsTrue(world.IsAllyAdvancing);
        Assert.AreEqual(10f - 0.02f, hold.X, 1e-5f);
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
        enemyHost.OnDiedCallback = self => world.Remove(self); // 사건 처리 중에 흐름 코드가 빼는 경우

        Assert.DoesNotThrow(() => Run(world, 10));
        Assert.IsNull(enemy.World);
        Assert.AreEqual(2, world.Combatants.Count);
    }
}
