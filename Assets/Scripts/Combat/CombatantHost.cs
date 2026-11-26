using System;
using System.Collections.Generic;
using UnityEngine;

// 전투원의 호스트(docs/COMBAT_ARCHITECTURE.md 8-3). 전투 규칙은 Combatant(순수 C#)가 갖고,
// 이 MonoBehaviour는 그리기(애니메이션, 위치 보간, HP바)와 Unity 연결(풀, 이벤트 버스)만 맡는다.
// PlayerController와 Monster(보스 포함)가 이것을 상속한다.
public abstract class CombatantHost : MonoBehaviour, ICombatantHost
{
    [Header("전투원")]
    [SerializeField] protected CharacterAnimationView view;
    [SerializeField] protected ActionTimelineSet timelines;
    [SerializeField] protected HitboxProfile hitboxProfile = new HitboxProfile();

    private readonly ReactiveProperty<HpState> hp = new ReactiveProperty<HpState>();

    public Combatant Combatant { get; private set; }

    // HP(현재, 최대)는 상태다. 머리 위 HP 바와 HUD가 같이 구독한다(docs/COMBAT_ARCHITECTURE.md 10-10 D29).
    public IReadOnlyReactiveProperty<HpState> Hp => hp;
    // 피격 깜빡임(HitEffect)은 사건이고 같은 오브젝트 안이라 C# 이벤트로 둔다(docs/UI_DESIGN.md 5-3 적용 경계).
    public event Action OnDamaged;

    protected abstract Team Team { get; }

    public bool IsAlive => Combatant != null && Combatant.IsAlive;
    public float HalfWidth => hitboxProfile.HalfWidth;
    public ActionTimelineSet Timelines => timelines;

    // 판정 몸의 중심(화면에 그려진 위치 기준). 이펙트, 데미지 숫자의 기준점.
    public Vector3 EffectPosition => transform.position + (Vector3)hitboxProfile.BodyOffset;

    public static Vector3 EffectPositionOf(Combatant combatant) =>
        combatant?.Host is CombatantHost host ? host.EffectPosition : Vector3.zero;

    protected virtual void Awake()
    {
        if (view == null) view = GetComponent<CharacterAnimationView>();
        if (view == null) Debug.LogError($"[{GetType().Name}] {name}에 CharacterAnimationView가 없습니다.");
        if (timelines == null) Debug.LogError($"[{GetType().Name}] {name}에 ActionTimelineSet이 연결되지 않았습니다.");

        Combatant = new Combatant(Team, this);
    }

    // 피벗 x → 판정 몸 중심 x
    protected float BodyCenterX(float pivotX) => pivotX + hitboxProfile.BodyOffset.x;

    // CombatSimulation이 화면 프레임마다 부른다. 직전 두 고정 단계 사이를 그린다(최대 0.02초 늦게 보임).
    public void ApplyRenderPose(float alpha)
    {
        if (Combatant == null || Combatant.Phase == CombatantPhase.Inactive) return;

        Vector3 position = transform.position;
        position.x = Combatant.GetRenderX(alpha) - hitboxProfile.BodyOffset.x;
        transform.position = position;
    }

    // ── ICombatantHost: 공통 연출 ──

    public virtual void HandleHpChanged(Combatant self) => PublishHp(self);

    public virtual void HandleDamaged(Combatant self, GameNumber amount)
    {
        PublishHp(self);
        OnDamaged?.Invoke();
        GlobalCombatEvents.RaiseTargetDamaged(self, amount, transform.position);
    }

    public virtual void HandleHealed(Combatant self, GameNumber amount)
    {
        PublishHp(self);
        GlobalCombatEvents.RaiseHealed(self, amount, transform.position);
    }

    private void PublishHp(Combatant self) => hp.Value = new HpState(self.Hp, self.MaxHp);

    // 기본: 달리기 동작이 없는 캐릭터(몬스터)는 이동 중에도 대기 동작이다. 플레이어가 덮어쓴다.
    public virtual void HandleLocomotion(Combatant self, bool moving)
    {
        if (view != null) view.PlayLocomotion(false);
    }

    public void HandleActionStarted(Combatant self, ActionTimeline action, double duration, Skill skill)
    {
        if (view != null) view.PlayAction(action, (float)duration);
        if (skill != null) SkillPresentation.PlayCast(skill, EffectPosition);
    }

    public void HandleBasicAttackHit(Combatant self, Combatant target)
    {
        if (CombatEffectManager.Instance != null) CombatEffectManager.Instance.SpawnHitParticle(target);
    }

    public void HandleSkillHit(Combatant self, Skill skill, IReadOnlyList<Combatant> hitTargets) =>
        SkillPresentation.PlayHit(skill, hitTargets);

    public virtual void HandleDied(Combatant self)
    {
        if (view != null) view.PlayDeath();
    }

    public virtual void HandleDeathCompleted(Combatant self) { }

    public virtual void HandleRevived(Combatant self)
    {
        PublishHp(self);
        if (view != null) view.PlayLocomotion(false);
    }
}
