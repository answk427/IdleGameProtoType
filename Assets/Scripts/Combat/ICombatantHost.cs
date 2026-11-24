using System.Collections.Generic;

// Combatant(순수 C# 전투 규칙)가 바깥에 알리는 일. 호스트(MonoBehaviour)가 구현해 연출과 Unity 연결을 맡는다
// (docs/COMBAT_ARCHITECTURE.md 8-3). 전투 규칙은 이 알림의 결과를 읽지 않는다: 연출을 바꾸거나 빼도 전투 결과가 같다(R3).
// 로직에서 연출을 떼어 이벤트로만 잇는 것은 5단계에서 한다. 그때까지 부르는 곳을 이 접점 하나로 모은다.
public interface ICombatantHost
{
    void HandleHpChanged(Combatant self);
    void HandleDamaged(Combatant self, GameNumber amount);
    void HandleHealed(Combatant self, GameNumber amount);

    // 이동 단계에 들어감(true) / 교전 중 행동이 없음(false)
    void HandleLocomotion(Combatant self, bool moving);
    void HandleActionStarted(Combatant self, ActionTimeline action, double duration, Skill skill);
    void HandleBasicAttackHit(Combatant self, Combatant target);
    void HandleSkillHit(Combatant self, Skill skill, IReadOnlyList<Combatant> hitTargets);

    void HandleDied(Combatant self);
    // 사망 행동(시간표)이 끝났다. 사망 시간표가 없는 전투원은 사망과 같은 단계에 온다.
    void HandleDeathCompleted(Combatant self);
    void HandleRevived(Combatant self);
}
