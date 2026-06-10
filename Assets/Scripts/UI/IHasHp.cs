using System;
using UnityEngine;

public interface IHasHp
{
    event Action<float, float> OnHpChanged;
}
