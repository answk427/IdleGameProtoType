using System;
using UnityEngine;

public interface IDamageable
{
    bool IsAlive { get; }
    event Action<int> OnDamaged;
    void TakeDamage(int damage);
}
