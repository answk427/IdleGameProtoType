using System;
using UnityEngine;

// 스테이지 전환 페이드(docs/COMBAT_ARCHITECTURE.md 10-5). 전환은 흐름(EncounterDirector)의 시뮬레이션 시간이고,
// 이것은 그 시간을 그리기만 한다: 알파는 전환 시작 0 → 가운데(스테이지가 바뀌는 순간) 1 → 끝 0.
// 시뮬레이션 시간이라 배속에서는 전환과 함께 빨라지고, 기기의 프레임률이 전환 동안 도는 전투 단계 수를 바꾸지 않는다.
// 전환 중에는 입력을 막는다(흐름 단계를 구독).
public class FadeController : MonoBehaviour
{
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    private EncounterDirector director;
    private InputBlocker inputBlocker;
    private IDisposable phaseSubscription;

    // 전환 중 쥐는 입력 차단. 구독 콜백이 아니라 이 객체가 들고 있어서 꺼질 때(OnDisable)도 푼다.
    private IDisposable inputLease;

    // 그리는 순서(System 구분)와 입력 차단은 UIRoot가 준다.
    public void Initialize(InputBlocker blocker, EncounterDirector flow)
    {
        inputBlocker = blocker;
        director = flow;
        phaseSubscription?.Dispose();
        phaseSubscription = director.Phase.Subscribe(HandlePhaseChanged);
    }

    private void Awake()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    private void OnDisable() => ReleaseInput();

    private void OnDestroy()
    {
        phaseSubscription?.Dispose();
        ReleaseInput();
    }

    private void HandlePhaseChanged(FlowPhase phase)
    {
        if (phase == FlowPhase.Transition)
        {
            if (inputLease == null) inputLease = inputBlocker?.Acquire("ScreenFade", showSpinner: false);
        }
        else
        {
            ReleaseInput();
        }
    }

    private void Update()
    {
        if (director == null || fadeCanvasGroup == null) return;

        fadeCanvasGroup.alpha = director.Phase.Value == FlowPhase.Transition
            ? FadeAlpha(director.Transition, director.Clock.Now)
            : 0f;
    }

    // 삼각형: 시작 0 → 가운데 1 → 끝 0. 단계 경계의 시각으로 그린다(최대 한 단계 늦다).
    public static float FadeAlpha(TransitionTimes times, double now)
    {
        if (now < times.Middle) return Mathf.Clamp01((float)((now - times.Start) / (times.Middle - times.Start)));
        if (times.End <= times.Middle) return now <= times.Middle ? 1f : 0f;
        return Mathf.Clamp01((float)((times.End - now) / (times.End - times.Middle)));
    }

    private void ReleaseInput()
    {
        inputLease?.Dispose();
        inputLease = null;
    }
}
