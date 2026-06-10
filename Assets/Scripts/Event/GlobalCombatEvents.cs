using System;
using UnityEngine;

public class GlobalCombatEvents
{
    public static event Action<IDamageable, int, Vector3> OnAnyTargetDamaged;

    public static void TriggerAnyTargetDamaged(IDamageable target, int damage, Vector3 position)
    {
        OnAnyTargetDamaged?.Invoke(target, damage, position);
    }
}
