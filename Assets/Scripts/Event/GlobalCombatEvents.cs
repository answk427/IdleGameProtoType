using UnityEngine;

// 전투 → 바깥(연출, 보상) 방향의 정적 이벤트 버스(docs/UI_DESIGN.md 5-3). 금액은 GameNumber(D5).
//
// - 정적인 것은 원칙의 예외다(풀에서 나오는 몬스터까지 참조를 넘기는 비용 대비 얻는 것이 적다). 알림 수단은 다른 경계 알림과 같이
//   사건(Subject)이고, 구독은 IDisposable 손잡이로 해제한다. 내용은 구조체라 발행에 할당이 없다.
// - 플레이를 시작할 때 새로 만든다. 도메인 리로드를 꺼도(Enter Play Mode Options) 지난 플레이의 파괴된 구독자에게 알림이 가지 않는다.
// - 흐름의 상태와 사건(스테이지, 보스 도전 가능)은 여기가 아니라 EncounterDirector에 있다.
// 로직에서 연출을 떼어 이벤트로만 잇는 정리는 5단계에서 한다(docs/COMBAT_ARCHITECTURE.md P7).
public static class GlobalCombatEvents
{
    public readonly struct Damage
    {
        public readonly Combatant Target;
        public readonly GameNumber Amount;
        public readonly Vector3 Position;

        public Damage(Combatant target, GameNumber amount, Vector3 position)
        {
            Target = target;
            Amount = amount;
            Position = position;
        }
    }

    public readonly struct MonsterDeath
    {
        public readonly Monster Monster;
        public readonly GameNumber GoldReward;
        public readonly Vector3 Position;

        public MonsterDeath(Monster monster, GameNumber goldReward, Vector3 position)
        {
            Monster = monster;
            GoldReward = goldReward;
            Position = position;
        }
    }

    public readonly struct Heal
    {
        public readonly Combatant Target;
        public readonly GameNumber Amount;
        public readonly Vector3 Position;

        public Heal(Combatant target, GameNumber amount, Vector3 position)
        {
            Target = target;
            Amount = amount;
            Position = position;
        }
    }

    private static Subject<Damage> targetDamaged = new Subject<Damage>();
    private static Subject<MonsterDeath> monsterDied = new Subject<MonsterDeath>();
    private static Subject<Heal> healed = new Subject<Heal>();

    // 아군·적 구분 없이 피해를 받음(데미지 숫자)
    public static Observable<Damage> TargetDamaged => targetDamaged;
    // 몬스터 사망(보상, 동전 연출)
    public static Observable<MonsterDeath> MonsterDied => monsterDied;
    // 회복(회복 숫자)
    public static Observable<Heal> Healed => healed;

    public static void RaiseTargetDamaged(Combatant target, GameNumber amount, Vector3 position) =>
        targetDamaged.OnNext(new Damage(target, amount, position));

    public static void RaiseMonsterDied(Monster monster, GameNumber goldReward, Vector3 position) =>
        monsterDied.OnNext(new MonsterDeath(monster, goldReward, position));

    public static void RaiseHealed(Combatant target, GameNumber amount, Vector3 position) =>
        healed.OnNext(new Heal(target, amount, position));

    // 구독자를 모두 버린다. 플레이를 시작할 때 자동으로 불린다(테스트도 쓴다).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetSubscribers()
    {
        targetDamaged = new Subject<Damage>();
        monsterDied = new Subject<MonsterDeath>();
        healed = new Subject<Heal>();
    }
}
