using System;
using UnityEngine;

// 전투원의 호스트(뷰, docs/COMBAT_ARCHITECTURE.md 8-3, 11절 5-A). 전투 규칙은 Combatant(순수 C#)가 갖고,
// 이 MonoBehaviour는 그리기(애니메이션, 위치 보간, HP 상태)와 Unity 연결만 맡는다. PlayerController와 Monster(보스 포함)가 상속한다.
//
// 전투원은 자기 뷰를 모른다. 뷰는 드라이버가 단계 끝에 배달하는 사건(CombatEvent)을 CombatantViews를 거쳐 받아 그린다.
// 이펙트·소리·데미지 숫자·보상은 여기가 아니라 그 사건의 다른 구독자(CombatEffectManager, GameManager)가 한다.
public abstract class CombatantHost : MonoBehaviour
{
    [Header("전투원")]
    [SerializeField] protected CharacterAnimationView view;
    [SerializeField] protected ActionTimelineSet timelines;
    [SerializeField] protected HitboxProfile hitboxProfile = new HitboxProfile();

    private readonly ReactiveProperty<HpState> hp = new ReactiveProperty<HpState>();
    private CombatantViews views;

    public Combatant Combatant { get; private set; }

    // HP(현재, 최대)는 상태다. 머리 위 HP 바와 HUD가 같이 구독한다(docs/COMBAT_ARCHITECTURE.md 10-10 D29).
    public IReadOnlyReactiveProperty<HpState> Hp => hp;
    // 피격 깜빡임(HitEffect)은 사건이고 같은 오브젝트 안이라 C# 이벤트로 둔다(docs/UI_DESIGN.md 5-3 적용 경계).
    public event Action OnDamaged;

    protected abstract Team Team { get; }

    public bool IsAlive => Combatant != null && Combatant.IsAlive;
    public float HalfWidth => hitboxProfile.HalfWidth;
    public ActionTimelineSet Timelines => timelines;

    // 판정 몸의 중심. x는 시뮬레이션의 위치라 원점 재설정 직후에도 맞는다(그려진 transform은 다음 화면 프레임에 따라온다).
    // 이펙트(타격, 스킬)의 기준점.
    public Vector3 EffectPosition => new Vector3(BodyX, transform.position.y + hitboxProfile.BodyOffset.y, transform.position.z);

    // 피벗(발 기준) 자리. 데미지 숫자, 보상 동전의 기준점.
    public Vector3 PivotPosition => new Vector3(BodyX - hitboxProfile.BodyOffset.x, transform.position.y, transform.position.z);

    private float BodyX => Combatant != null && Combatant.Phase != CombatantPhase.Inactive
        ? Combatant.X
        : transform.position.x + hitboxProfile.BodyOffset.x;

    protected virtual void Awake()
    {
        if (view == null) view = GetComponent<CharacterAnimationView>();
        if (view == null) Debug.LogError($"[{GetType().Name}] {name}에 CharacterAnimationView가 없습니다.");
        if (timelines == null) Debug.LogError($"[{GetType().Name}] {name}에 ActionTimelineSet이 연결되지 않았습니다.");

        Combatant = new Combatant(Team);
    }

    protected virtual void OnDestroy() => views?.Unregister(this);

    // CombatantViews가 등록할 때 부른다(파괴될 때 빠지려고 기억한다).
    internal void BindViews(CombatantViews owner) => views = owner;

    // 피벗 x → 판정 몸 중심 x
    protected float BodyCenterX(float pivotX) => pivotX + hitboxProfile.BodyOffset.x;

    // 드라이버가 화면 프레임마다 부른다. 직전 두 고정 단계 사이를 그린다(최대 0.02초 늦게 보임).
    public void ApplyRenderPose(float alpha)
    {
        if (Combatant == null || Combatant.Phase == CombatantPhase.Inactive) return;

        Vector3 position = transform.position;
        position.x = Combatant.GetRenderX(alpha) - hitboxProfile.BodyOffset.x;
        transform.position = position;
    }

    // ── 사건(이 전투원이 주인인 것만 온다) ──

    public virtual void HandleEvent(in CombatEvent e)
    {
        switch (e.Type)
        {
            case CombatEventType.Joined:
                PublishHp();
                HandleLocomotion(true); // 스폰하면 이동 단계에서 시작한다
                break;
            case CombatEventType.StatsChanged:
            case CombatEventType.Healed:
                PublishHp();
                break;
            case CombatEventType.Damaged:
                PublishHp();
                OnDamaged?.Invoke();
                break;
            case CombatEventType.Locomotion:
                HandleLocomotion(e.Moving);
                break;
            case CombatEventType.ActionStarted:
                if (view != null && timelines != null && timelines.TryGet(e.ActionKey, out ActionTimeline action))
                    view.PlayAction(action, (float)e.Duration);
                break;
            case CombatEventType.Died:
                HandleDied();
                break;
            case CombatEventType.Revived:
                PublishHp();
                if (view != null) view.PlayLocomotion(false);
                break;
        }
    }

    // 지금 HP를 그린다. 배달은 단계 끝이라 같은 단계의 피해가 여러 번이어도 마지막 값이 보인다.
    protected void PublishHp()
    {
        if (Combatant != null) hp.Value = new HpState(Combatant.Hp, Combatant.MaxHp);
    }

    // 기본: 달리기 동작이 없는 캐릭터(몬스터)는 이동 중에도 대기 동작이다. 플레이어가 덮어쓴다.
    protected virtual void HandleLocomotion(bool moving)
    {
        if (view != null) view.PlayLocomotion(false);
    }

    protected virtual void HandleDied()
    {
        if (view != null) view.PlayDeath();
    }
}
