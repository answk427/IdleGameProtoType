using System.Collections.Generic;
using UnityEngine;

// ISkillBehavior가 caster에게 요구하는 동작만 모은 인터페이스.
// PlayerController/BossMonster처럼 스킬을 쓰는 주체가 늘어나도
// ISkillBehavior 구현체(DamageSkillBehavior 등)는 그대로 재사용된다.
public interface ISkillCaster
{
    Vector3 Position { get; }
    int GetCalculatedDamage(float skillMultiplier);
    void Heal(int amount);
    List<IDamageable> GetOpponentsInRange(float radius);
}
