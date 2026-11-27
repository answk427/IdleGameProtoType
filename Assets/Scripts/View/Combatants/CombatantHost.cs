using System;
using UnityEngine;
using UnityEngine.Serialization;

// 전투원의 호스트(뷰, docs/COMBAT_ARCHITECTURE.md 8-3, 11절 5-A). 전투 규칙은 Combatant(순수 C#)가 갖고,
// 이 MonoBehaviour는 그리기(애니메이션, 위치 보간, HP 상태)와 Unity 연결만 맡는다. PlayerController와 Monster(보스 포함)가 상속한다.
//
// 전투원의 주인은 시뮬레이션이다(docs/COMBAT_ARCHITECTURE.md 12절 D44): 이 뷰는 전투원을 만들지도, 월드를 바꾸지도 않는다.
// BattleView가 월드의 Joined에 이 뷰를 전투원에 붙이고(Bind) Left에 뗀다(Unbind). 풀에서 다시 꺼내면 다른 전투원에 붙을 수 있다.
// 전투원은 자기 뷰를 모른다. 뷰는 루프(SimulationLoop)가 단계 끝에 배달하는 사건(CombatEvent)을 CombatantViews를 거쳐 받아 그린다.
// 이펙트·소리·데미지 숫자·보상은 여기가 아니라 그 사건의 다른 구독자(CombatEffectManager, Game)가 한다.
// 판정 몸의 반너비와 행동 시간표는 게임 데이터다(12절 D50). 여기는 그림의 몸 기준점(BodyAnchor)만 갖는다.
public abstract class CombatantHost : MonoBehaviour
{
    [Header("전투원")]
    [SerializeField] protected CharacterAnimationView view;
    [FormerlySerializedAs("hitboxProfile")]
    [SerializeField] protected BodyAnchor bodyAnchor = new BodyAnchor();

    private readonly ReactiveProperty<HpState> hp = new ReactiveProperty<HpState>();
    private CombatantViews views;

    // 그리는 전투원(읽기 전용 접점, 13-6). 뷰는 전투원을 바꾸지 않는다.
    public IReadOnlyCombatant Combatant { get; private set; }

    // HP(현재, 최대)는 상태다. 머리 위 HP 바와 HUD가 같이 구독한다(docs/COMBAT_ARCHITECTURE.md 10-10 D29).
    public IReadOnlyReactiveProperty<HpState> Hp => hp;
    // 피격 깜빡임(HitEffect)은 사건이고 같은 오브젝트 안이라 C# 이벤트로 둔다(docs/UI_DESIGN.md 5-3 적용 경계).
    public event Action OnDamaged;

    public bool IsAlive => Combatant != null && Combatant.IsAlive;
    // 그림의 몸 기준점(피벗 → 몸 중심). BattleView가 붙일 때 피벗 위치를 계산한다.
    public BodyAnchor Anchor => bodyAnchor;

    // 판정 몸의 중심. x는 시뮬레이션의 위치라 원점 재설정 직후에도 맞는다(그려진 transform은 다음 화면 프레임에 따라온다).
    // 이펙트(타격, 스킬)의 기준점.
    public Vector3 EffectPosition => new Vector3(BodyX, transform.position.y + bodyAnchor.BodyOffset.y, transform.position.z);

    // 피벗(발 기준) 자리. 데미지 숫자, 보상 동전의 기준점.
    public Vector3 PivotPosition => new Vector3(BodyX - bodyAnchor.BodyOffset.x, transform.position.y, transform.position.z);

    private float BodyX => Combatant != null && Combatant.Phase != CombatantPhase.Inactive
        ? Combatant.X
        : transform.position.x + bodyAnchor.BodyOffset.x;

    protected virtual void Awake()
    {
        if (view == null) view = GetComponent<CharacterAnimationView>();
        if (view == null) Debug.LogError($"[{GetType().Name}] {name}에 CharacterAnimationView가 없습니다.");
    }

    protected virtual void OnDestroy() => views?.Forget(this);

    // BattleView가 Joined에 부른다. 처음 상태(HP, 이동 동작)는 이어서 오는 Joined 사건이 그린다.
    public virtual void Bind(IReadOnlyCombatant combatant) => Combatant = combatant;

    // BattleView가 Left에 부른다(풀로 돌려놓기 전).
    public virtual void Unbind() => Combatant = null;

    // CombatantViews가 등록할 때 부른다(파괴될 때 빠지려고 기억한다).
    internal void BindViews(CombatantViews owner) => views = owner;

    // BattleView가 화면 프레임마다 부른다. 직전 두 고정 단계 사이를 그린다(최대 0.02초 늦게 보임).
    public void ApplyRenderPose(float alpha)
    {
        if (Combatant == null || Combatant.Phase == CombatantPhase.Inactive) return;

        Vector3 position = transform.position;
        position.x = Combatant.GetRenderX(alpha) - bodyAnchor.BodyOffset.x;
        transform.position = position;
    }

    // ── 사건(이 전투원이 주인인 것만 온다) ──

    public virtual void HandleEvent(in CombatEvent e)
    {
        if (e.ChangesHp) PublishHp();

        switch (e.Type)
        {
            case CombatEventType.Joined:
                HandleLocomotion(true); // 스폰하면 이동 단계에서 시작한다
                break;
            case CombatEventType.Damaged:
                OnDamaged?.Invoke();
                break;
            case CombatEventType.Locomotion:
                HandleLocomotion(e.Moving);
                break;
            case CombatEventType.ActionStarted:
                // 시간표는 그 전투원의 것(데이터)이다. 클립 길이로 재생 속도를 맞춘다.
                if (view != null && Combatant != null && Combatant.TryGetTiming(e.ActionKey, out ActionTiming action))
                    view.PlayAction(action, (float)e.Duration);
                break;
            case CombatEventType.Died:
                HandleDied();
                break;
            case CombatEventType.Revived:
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
