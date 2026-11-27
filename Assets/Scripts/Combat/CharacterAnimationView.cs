using UnityEngine;

// 캐릭터 애니메이션의 재생기(docs/COMBAT_ARCHITECTURE.md 3-5, 7-5).
// 전투 로직이 정한 행동과 길이를 보여 주기만 하고, 전투 로직은 애니메이터에서 아무것도 읽지 않는다.
// 컨트롤러는 조건 전이 없는 상태 모음이다. 상태는 이름으로 직접 재생하고, 행동 상태의 속도는 ActionSpeed 파라미터로 맞춘다.
// Animator Update Mode는 Normal(화면 프레임에 맞춰 부드럽게). 로직이 애니메이션을 읽지 않으므로 컬링 모드도 전투와 무관하다.
[RequireComponent(typeof(Animator))]
public class CharacterAnimationView : MonoBehaviour
{
    private static readonly int ActionSpeedParam = Animator.StringToHash("ActionSpeed");

    [SerializeField] private Animator animator;
    [SerializeField] private string idleState = "Idle";
    [Tooltip("비우면 달리기 동작이 없는 캐릭터로 보고 대기 동작을 쓴다")]
    [SerializeField] private string runState = "Run";
    [SerializeField] private string deathState = "Death";
    [Tooltip("행동이 끝나고 대기/달리기로 돌아갈 때 섞는 시간(초)")]
    [SerializeField] private float locomotionBlend = 0.05f;

    private int currentLocomotionHash;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    // 풀에서 다시 꺼낸 캐릭터가 예전 상태로 남지 않게 한다.
    private void OnEnable()
    {
        currentLocomotionHash = 0;
    }

    // 행동 상태를 처음부터 재생하고, 클립이 정확히 duration초 동안 재생되도록 속도를 맞춘다.
    public void PlayAction(ActionTiming action, float duration)
    {
        if (animator == null || action == null || !(duration > 0f)) return;

        animator.SetFloat(ActionSpeedParam, action.ClipLength / duration);
        animator.Play(action.Key, 0, 0f);
        currentLocomotionHash = 0;
    }

    public void PlayLocomotion(bool running)
    {
        if (animator == null) return;

        string state = running && !string.IsNullOrEmpty(runState) ? runState : idleState;
        int hash = Animator.StringToHash(state);
        if (hash == currentLocomotionHash) return;

        currentLocomotionHash = hash;
        animator.CrossFadeInFixedTime(hash, locomotionBlend, 0);
    }

    public void PlayDeath()
    {
        if (animator == null || string.IsNullOrEmpty(deathState)) return;

        animator.Play(deathState, 0, 0f);
        currentLocomotionHash = 0;
    }

    // ── 저작용 표식 ──
    // 클립의 AE_OnHit 이벤트는 타격 비율로 굽는 원천이다(굽기 도구). 런타임에는 애니메이터가 계속 부르므로
    // 받는 메서드가 있어야 "receiver 없음" 에러가 나지 않는다. 여기서는 아무것도 하지 않는다.
    public void AE_OnHit() { }
}
