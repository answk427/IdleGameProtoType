public interface ISkillBehavior
{
    // 반환값은 효과가 실제로 적용된 위치
    UnityEngine.Vector3 Execute(ISkillCaster caster, IDamageable target);
}
