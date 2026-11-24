using System.Collections.Generic;

// 스킬 효과 하나(데미지, 범위 데미지, 회복). 새 효과는 구현체 하나 + Skill.CreateBehavior 분기 하나를 더한다.
public interface ISkillBehavior
{
    // target: 시전자가 지금 잡고 있는 대상(없으면 null). 효과가 실제로 적용된 전투원을 hits에 더한다(연출 위치의 기준).
    // hits는 부르는 쪽이 재사용하는 버퍼다(전투 중 할당 없음, docs/COMBAT_ARCHITECTURE.md 8-6).
    void Execute(Combatant caster, Combatant target, List<Combatant> hits);
}
