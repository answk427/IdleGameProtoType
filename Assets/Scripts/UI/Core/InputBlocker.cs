using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// 참조 카운트 입력 차단 + 지연 스피너 (docs/UI_DESIGN.md 7장).
//
//   using (inputBlocker.Acquire("이유")) { await ...; }   // async + destroyCancellationToken에서만
//
// 코루틴의 지역변수로 using하지 말 것. StopCoroutine이나 파괴로 끊기면 finally가 실행되지 않는다.
// 그때는 수명이 있는 객체가 필드로 들고 OnDisable에서도 해제한다(FadeController 참고).
public sealed class InputBlocker
{
    private const float SpinnerDelay = 0.3f;
    private const float WarningAfter = 10f;
    private const float SpinnerSize = 80f;
    private const float SpinnerDegreesPerSecond = 180f;

    private readonly Canvas canvas;
    private readonly GraphicRaycaster raycaster;
    private readonly Image spinner;

    private readonly List<Lease> leases = new List<Lease>();
    private readonly ReactiveProperty<bool> isBlocked = new ReactiveProperty<bool>();

    public IReadOnlyReactiveProperty<bool> IsBlocked => isBlocked;

    public InputBlocker(Transform uiRoot)
    {
        canvas = UILayerFactory.CreateLayer("InputBlocker", uiRoot, UISort.InputBlockOrder);
        raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();

        Image blockImage = UILayerFactory.CreateFullScreenImage("Block", canvas.transform, Color.clear);

        // 전용 아트가 생기기 전까지의 자리표시 스피너.
        spinner = UILayerFactory.CreateFullScreenImage("Spinner", blockImage.transform, new Color(1f, 1f, 1f, 0.8f));
        spinner.raycastTarget = false;
        RectTransform rect = spinner.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(SpinnerSize, SpinnerSize);
        spinner.enabled = false;

        canvas.enabled = false;
        raycaster.enabled = false;
    }

    // 차단은 즉시, 스피너는 SpinnerDelay를 넘을 때만. 자체 연출이 있는 차단(화면 페이드)은 스피너를 끈다.
    // warnIfLong: 오래 풀리지 않으면 경고한다(영구 잠금 디버깅용). 사용자가 닫을 때까지 쥐는 차단
    // (개발 도구 패널, 이후 튜토리얼)은 길어도 정상이라 끈다.
    public IDisposable Acquire(string reason, bool showSpinner = true, bool warnIfLong = true)
    {
        var lease = new Lease(this, reason, showSpinner) { Warned = !warnIfLong };
        leases.Add(lease);

        if (leases.Count == 1)
        {
            SetVisible(true);
            UILayerFactory.ClearSelection();
            isBlocked.Value = true;
        }

        return lease;
    }

    // UIRoot가 매 프레임 부른다. 게임 시간이 멈춰도 움직이도록 unscaled 시간을 쓴다.
    public void Tick()
    {
        if (leases.Count == 0 || spinner == null) return;

        float now = Time.unscaledTime;
        bool showSpinner = false;

        foreach (Lease lease in leases)
        {
            float elapsed = now - lease.StartedAt;
            if (lease.ShowSpinner && elapsed >= SpinnerDelay) showSpinner = true;

            if (!lease.Warned && elapsed >= WarningAfter)
            {
                lease.Warned = true;
                string all = string.Join(", ", leases.Select(item => item.Reason));
                Debug.LogWarning($"[InputBlocker] '{lease.Reason}' 차단이 {WarningAfter}초 넘게 풀리지 않았습니다. 현재 차단: {all}");
            }
        }

        spinner.enabled = showSpinner;
        if (showSpinner) spinner.rectTransform.Rotate(0f, 0f, -SpinnerDegreesPerSecond * Time.unscaledDeltaTime);
    }

    // 디버그 표시용: 지금 차단을 쥔 이유와 지난 시간(영구 잠금을 찾을 때 본다).
    public void Describe(UIDebugSnapshot into)
    {
        float now = Time.unscaledTime;
        foreach (Lease lease in leases) into.Blocks.Add(new UIDebugSnapshot.BlockInfo(lease.Reason, now - lease.StartedAt));
    }

    private void Release(Lease lease)
    {
        if (!leases.Remove(lease) || leases.Count > 0) return;

        SetVisible(false);
        isBlocked.Value = false;
    }

    private void SetVisible(bool visible)
    {
        // 앱이 꺼지는 중이면 이미 파괴돼 있을 수 있다.
        if (canvas == null) return;

        canvas.enabled = visible;
        raycaster.enabled = visible;
        if (!visible) spinner.enabled = false;
    }

    private sealed class Lease : IDisposable
    {
        private InputBlocker owner;

        public readonly string Reason;
        public readonly bool ShowSpinner;
        public readonly float StartedAt;
        public bool Warned;

        public Lease(InputBlocker owner, string reason, bool showSpinner)
        {
            this.owner = owner;
            Reason = reason;
            ShowSpinner = showSpinner;
            StartedAt = Time.unscaledTime;
        }

        public void Dispose()
        {
            InputBlocker current = owner;
            owner = null;
            current?.Release(this);
        }
    }
}
