// 전투 시뮬레이션이 바깥에 알리는 일(docs/COMBAT_ARCHITECTURE.md 3-1, 11절 5-A). 순수 C#.
//
// - 시뮬레이션은 뷰·이펙트·소리·보상을 부르지 않는다(P7). 일어난 일을 사건으로 월드의 버퍼에 쌓기만 하고,
//   루프(SimulationLoop)가 고정 단계가 끝난 뒤에 구독자에게 배달한다(CombatWorld.DeliverEvents). 그래서 연출이 시뮬레이션 처리 도중에
//   끼어들 수 없고(재진입 없음), 연출을 바꾸거나 빼도 전투 결과가 같다(R3).
// - 사건에는 일어난 시각(시뮬레이션 시각)이 있다. 배달은 단계 끝이지만 순서와 시각은 처리 그대로다.
// - 구조체 하나에 종류별 값을 담는다(발행에 할당이 없다). 종류에 해당하지 않는 칸은 기본값이다.
public enum CombatEventType : byte
{
    Joined,          // 월드에 들어왔다(스폰·풀에서 다시 꺼냄). 뷰가 처음 상태(HP, 이동 동작)를 그린다
    StatsChanged,    // 능력치가 바뀌었다(레벨업, 업그레이드). 최대 HP가 바뀔 수 있다
    Damaged,         // Amount = 받은 피해(0일 수 있다)
    Healed,          // Amount = 회복량
    Locomotion,      // Moving = 이동 단계에 들어감(true) / 교전 중 행동이 없음(false)
    ActionStarted,   // ActionKey, Duration(초), SkillId(기본 공격이면 0)
    BasicAttackHit,  // Other = 맞은 대상
    SkillHit,        // SkillId, Other = 맞은 대상(맞은 수만큼 하나씩)
    Died,            // Gold, Exp = 처치 보상(스폰할 때 정한 값, 없으면 0)
    DeathCompleted,  // 사망 행동(시간표)이 끝났다
    Revived,         // 스테이지를 다시 시작해 처음 상태로
    OriginShifted,   // Delta = 모든 좌표에서 뺀 거리(원점 재설정). Source는 없다
    Left,            // 월드에서 빠졌다(정리, 풀로 돌아감). 뷰가 떨어진다. 같은 전투원이 다시 Joined로 들어올 수 있다(12절 D44)
}

public readonly struct CombatEvent
{
    public readonly CombatEventType Type;
    public readonly double Time;
    public readonly Combatant Source;
    public readonly Combatant Other;
    public readonly GameNumber Amount;
    public readonly bool Moving;
    public readonly string ActionKey;
    public readonly double Duration;
    public readonly int SkillId;
    public readonly GameNumber Gold;
    public readonly int Exp;
    public readonly float Delta;

    // 이 사건 뒤에 전투원의 HP(현재, 최대)가 달라졌을 수 있다. HP를 그리는 쪽(머리 위 바, HUD)이 같은 규칙을 쓴다.
    public bool ChangesHp => Type == CombatEventType.Joined || Type == CombatEventType.StatsChanged || Type == CombatEventType.Damaged
        || Type == CombatEventType.Healed || Type == CombatEventType.Revived;

    private CombatEvent(CombatEventType type, double time, Combatant source, Combatant other = null, GameNumber amount = default,
        bool moving = false, string actionKey = null, double duration = 0.0, int skillId = 0, GameNumber gold = default, int exp = 0,
        float delta = 0f)
    {
        Type = type;
        Time = time;
        Source = source;
        Other = other;
        Amount = amount;
        Moving = moving;
        ActionKey = actionKey;
        Duration = duration;
        SkillId = skillId;
        Gold = gold;
        Exp = exp;
        Delta = delta;
    }

    public static CombatEvent Joined(double time, Combatant source) => new CombatEvent(CombatEventType.Joined, time, source);
    public static CombatEvent Left(double time, Combatant source) => new CombatEvent(CombatEventType.Left, time, source);
    public static CombatEvent StatsChanged(double time, Combatant source) => new CombatEvent(CombatEventType.StatsChanged, time, source);
    public static CombatEvent Damaged(double time, Combatant source, GameNumber amount) => new CombatEvent(CombatEventType.Damaged, time, source, amount: amount);
    public static CombatEvent Healed(double time, Combatant source, GameNumber amount) => new CombatEvent(CombatEventType.Healed, time, source, amount: amount);
    public static CombatEvent Locomotion(double time, Combatant source, bool moving) => new CombatEvent(CombatEventType.Locomotion, time, source, moving: moving);

    public static CombatEvent ActionStarted(double time, Combatant source, string actionKey, double duration, int skillId) =>
        new CombatEvent(CombatEventType.ActionStarted, time, source, actionKey: actionKey, duration: duration, skillId: skillId);

    public static CombatEvent BasicAttackHit(double time, Combatant source, Combatant target) =>
        new CombatEvent(CombatEventType.BasicAttackHit, time, source, other: target);

    public static CombatEvent SkillHit(double time, Combatant source, int skillId, Combatant target) =>
        new CombatEvent(CombatEventType.SkillHit, time, source, other: target, skillId: skillId);

    public static CombatEvent Died(double time, Combatant source, in CombatReward reward) =>
        new CombatEvent(CombatEventType.Died, time, source, gold: reward.Gold, exp: reward.Exp);

    public static CombatEvent DeathCompleted(double time, Combatant source) => new CombatEvent(CombatEventType.DeathCompleted, time, source);
    public static CombatEvent Revived(double time, Combatant source) => new CombatEvent(CombatEventType.Revived, time, source);
    public static CombatEvent OriginShifted(double time, float delta) => new CombatEvent(CombatEventType.OriginShifted, time, null, delta: delta);

    public override string ToString() => $"{Type} t={Time} {Source?.Team} {Source?.Order}";
}

// 처치 보상. 스폰할 때 전투원에 넣고 사망 사건이 싣고 간다(게임 쪽이 지급한다). 지금은 몬스터만 갖는다.
public readonly struct CombatReward
{
    public readonly GameNumber Gold;
    public readonly int Exp;

    public CombatReward(GameNumber gold, int exp)
    {
        Gold = gold;
        Exp = exp;
    }

    public static CombatReward None => default;
}
