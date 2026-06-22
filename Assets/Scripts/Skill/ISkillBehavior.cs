public interface ISkillBehavior
{
    // 반환값은 효과가 실제로 적용된 위치 — 효과별로 대상이 다르므로(타겟 vs 캐스터 자신 등)
    // 호출자가 target으로 추측하지 않고 이 값을 그대로 VFX/SFX 위치에 사용한다.
    UnityEngine.Vector3 Execute(PlayerController caster, IDamageable target);
}
