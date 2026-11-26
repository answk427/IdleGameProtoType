using System.Collections.Generic;

// 전투원 테스트 도구: 씬 없이 CombatWorld와 Combatant를 돌린다(docs/COMBAT_ARCHITECTURE.md 8-10).
public static class CombatTestKit
{
    public const double Step = 0.02;

    // 연출 대신 그 전투원이 주인인 사건(CombatEvent, 5-A)의 수만 센다(할당 측정 중에도 할당하지 않게 값만 기록).
    // 사건은 배달(world.DeliverEvents, StepOnce가 부른다) 때 온다. 단계 밖에서 직접 부른 명령(TakeDamage 등) 뒤에는 Deliver를 부른다.
    public sealed class FakeHost
    {
        public int Joined, StatsChanged, Damaged, Healed, Died, DeathCompleted, Revived, BasicHits, SkillHits, ActionsStarted;
        public GameNumber LastDamage;
        public CombatReward LastReward;
        public System.Action<Combatant, CombatEvent> OnDiedCallback;

        public void Record(CombatEvent e)
        {
            switch (e.Type)
            {
                case CombatEventType.Joined: Joined++; break;
                case CombatEventType.StatsChanged: StatsChanged++; break;
                case CombatEventType.Damaged: Damaged++; LastDamage = e.Amount; break;
                case CombatEventType.Healed: Healed++; break;
                case CombatEventType.ActionStarted: ActionsStarted++; break;
                case CombatEventType.BasicAttackHit: BasicHits++; break;
                case CombatEventType.SkillHit: SkillHits++; break; // 맞은 대상 하나마다 하나
                case CombatEventType.Died: Died++; LastReward = new CombatReward(e.Gold, e.Exp); OnDiedCallback?.Invoke(e.Source, e); break;
                case CombatEventType.DeathCompleted: DeathCompleted++; break;
                case CombatEventType.Revived: Revived++; break;
            }
        }
    }

    // 그 전투원의 사건을 host에 센다(테스트 준비 때 한 번 구독한다).
    public static void Attach(CombatWorld world, Combatant combatant, FakeHost host) =>
        world.Events.Subscribe(e => { if (e.Source == combatant) host.Record(e); });

    public static void Deliver(CombatWorld world) => world.DeliverEvents();

    public sealed class FakeTimelines : IActionTimelineSource
    {
        public readonly Dictionary<string, ActionTimeline> Map = new Dictionary<string, ActionTimeline>();

        public bool TryGet(string key, out ActionTimeline timeline) => Map.TryGetValue(key, out timeline);
    }

    public static FakeTimelines Timelines(bool attack = true, bool death = false, bool skill = true)
    {
        var t = new FakeTimelines();
        if (attack) t.Map["Attack"] = ActionTimeline.FromValues("Attack", 1f, new[] { 0.5f });
        if (skill) t.Map["Skill"] = ActionTimeline.FromValues("Skill", 1f, new[] { 0.5f });
        if (death) t.Map["Death"] = ActionTimeline.FromValues("Death", 1f, new float[0]);
        return t;
    }

    public static CombatantStats Stats(double hp = 100, double damage = 10, double interval = 0, float range = 0f,
        MovePolicy policy = MovePolicy.Hold, float speed = 0f) => new CombatantStats
    {
        MaxHp = GameNumber.FromDouble(hp),
        AttackDamage = GameNumber.FromDouble(damage),
        AttackInterval = interval,
        AttackRange = range,
        MoveSpeed = speed,
        MovePolicy = policy,
    };

    public static CombatWorld World() => new CombatWorld(new SimClock(Step));

    public static Combatant Spawn(CombatWorld world, Team team, float x, float halfWidth, CombatantStats stats,
        IActionTimelineSource timelines = null, SkillLoadout skills = null, FakeHost host = null, CombatReward reward = default)
    {
        var combatant = new Combatant(team);
        combatant.Spawn(stats, x, halfWidth, timelines ?? Timelines(), skills, reward);
        world.Add(combatant);
        if (host != null) Attach(world, combatant, host);
        return combatant;
    }

    public static SkillLoadout Loadout(params Skill[] skills)
    {
        var byId = new Dictionary<int, SkillEntry>();
        var ids = new List<int>();
        foreach (Skill skill in skills)
        {
            byId[skill.Data.Id] = skill.Entry;
            ids.Add(skill.Data.Id);
        }
        var loadout = new SkillLoadout(ids.Count, id => byId.TryGetValue(id, out SkillEntry e) ? e : null);
        loadout.SetSlots(ids);
        return loadout;
    }

    // 고정 단계 한 번(CombatSimulation의 국면 순서와 같다: ② → ③ → ⑤ → ⑥ 배달)
    public static void StepOnce(CombatWorld world)
    {
        world.RunEvents();
        world.StepPhases();
        world.CaptureStep();
        world.Clock.AdvanceStep();
        world.DeliverEvents();
    }

    public static void Run(CombatWorld world, int steps)
    {
        for (int i = 0; i < steps; i++) StepOnce(world);
    }
}
