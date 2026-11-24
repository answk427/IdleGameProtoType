using System;
using UnityEngine;

// 전투 → 바깥(연출, 보상) 방향의 정적 이벤트 버스. 금액은 GameNumber(D5).
// 로직에서 연출을 떼어 이벤트로만 잇는 정리는 5단계에서 한다(docs/COMBAT_ARCHITECTURE.md P7).
public class GlobalCombatEvents
{
    public static event Action<Combatant, GameNumber, Vector3> OnAnyTargetDamaged;
    public static event Action<Monster, GameNumber, Vector3> OnMonsterDied;
    public static event Action<GameNumber, Vector3> OnHealed;

    public static void TriggerAnyTargetDamaged(Combatant target, GameNumber damage, Vector3 position)
    {
        OnAnyTargetDamaged?.Invoke(target, damage, position);
    }

    public static void TriggerMonsterDied(Monster monster, GameNumber goldReward, Vector3 position)
    {
        OnMonsterDied?.Invoke(monster, goldReward, position);
    }

    public static void TriggerHealed(GameNumber amount, Vector3 position)
    {
        OnHealed?.Invoke(amount, position);
    }
}
