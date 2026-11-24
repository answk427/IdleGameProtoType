using System.Collections.Generic;

// 전투원 테스트 도구: 씬 없이 CombatWorld와 Combatant를 돌린다(docs/COMBAT_ARCHITECTURE.md 8-10).
public static class CombatTestKit
{
    public const double Step = 0.02;

    // 연출 대신 호출 횟수만 센다(할당 측정 중에도 할당하지 않게 값만 기록).
    public sealed class FakeHost : ICombatantHost
    {
        public int HpChanged, Damaged, Healed, Died, DeathCompleted, Revived, BasicHits, SkillHits, ActionsStarted;
        public int LastSkillHitCount;
        public GameNumber LastDamage;
        public System.Action<Combatant> OnDiedCallback;

        public void HandleHpChanged(Combatant self) => HpChanged++;
        public void HandleDamaged(Combatant self, GameNumber amount) { Damaged++; LastDamage = amount; }
        public void HandleHealed(Combatant self, GameNumber amount) => Healed++;
        public void HandleLocomotion(Combatant self, bool moving) { }
        public void HandleActionStarted(Combatant self, ActionTimeline action, double duration, Skill skill) => ActionsStarted++;
        public void HandleBasicAttackHit(Combatant self, Combatant target) => BasicHits++;
        public void HandleSkillHit(Combatant self, Skill skill, IReadOnlyList<Combatant> hitTargets) { SkillHits++; LastSkillHitCount = hitTargets.Count; }
        public void HandleDied(Combatant self) { Died++; OnDiedCallback?.Invoke(self); }
        public void HandleDeathCompleted(Combatant self) => DeathCompleted++;
        public void HandleRevived(Combatant self) => Revived++;
    }

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
        IActionTimelineSource timelines = null, SkillLoadout skills = null, FakeHost host = null)
    {
        var combatant = new Combatant(team, host ?? new FakeHost());
        combatant.Spawn(stats, x, halfWidth, timelines ?? Timelines(), skills);
        world.Add(combatant);
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

    // 고정 단계 한 번(CombatSimulation의 국면 순서와 같다)
    public static void StepOnce(CombatWorld world)
    {
        world.RunEvents();
        world.StepPhases();
        world.CaptureStep();
        world.Clock.AdvanceStep();
    }

    public static void Run(CombatWorld world, int steps)
    {
        for (int i = 0; i < steps; i++) StepOnce(world);
    }
}
