using System;
using UnityEngine;

public interface IDamageable
{
    bool IsAlive { get; }
    Vector3 Position { get; }
    event Action<int> OnDamaged;
    void TakeDamage(int damage);
}
