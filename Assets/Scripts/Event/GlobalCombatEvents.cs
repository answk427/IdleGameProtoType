using System;
using UnityEngine;

public class GlobalCombatEvents
{
    public static event Action<IDamageable, int, Vector3> OnAnyTargetDamaged;
    public static event Action<Monster, int, Vector3> OnMonsterDied;

    public static void TriggerAnyTargetDamaged(IDamageable target, int damage, Vector3 position)
    {
        OnAnyTargetDamaged?.Invoke(target, damage, position);
    }

    public static void TriggerMonsterDied(Monster monster, int goldReward, Vector3 position)
    {
        OnMonsterDied?.Invoke(monster, goldReward, position);
    }
}
