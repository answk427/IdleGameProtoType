using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 새 화면 UI의 진입점이자 조립 지점 (docs/UI_DESIGN.md 3장). GameInitializer가 Initialize로 주입한다.
//
// 이관 기간에는 기존 UIManager(HUD, 탭)와 공존한다. 규칙:
// - 화면 하나의 주인은 하나다. 지금 WindowManager의 창은 SkillManageWindow뿐이다.
// - 두 시스템을 아는 곳은 여기뿐이다("이관 기간 연결"). 이관이 끝나면 그 부분만 지우면 된다.
// - 이 Canvas(order 1)가 기존 UIManager Canvas(0)보다 항상 위다.
[RequireComponent(typeof(Canvas))]
public class UIRoot : MonoBehaviour
{
    [SerializeField] private WindowRegistry registry;

    [Header("System 구분")]
    [SerializeField] private Canvas fadeLayer;
    [SerializeField] private FadeController fadeController;

    [Header("이관 기간 연결 (기존 UIManager 쪽)")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private SkillSlotBar skillSlotBar;

    private readonly CompositeDisposable disposables = new CompositeDisposable();

    private WindowManager windows;
    private InputBlocker inputBlocker;
    private InputAction backAction;

    // Initialize 뒤에만 있다. 창이 아닌 화면(개발 도구 패널)도 같은 입력 차단을 쓴다.
    public InputBlocker InputBlocker => inputBlocker;

    public void Initialize(GameSession session, IReadOnlyList<SkillEntry> skills, PlayerController player)
    {
        if (windows != null || !ValidateReferences()) return;

        UISort.Apply(fadeLayer, UISort.FadeOrder);

        inputBlocker = new InputBlocker(transform);
        windows = new WindowManager(transform, registry, inputBlocker);
        fadeController.Initialize(inputBlocker);

        BindWindows(session, skills, player);

        inputBlocker.IsBlocked.Subscribe(_ => RefreshNavigation()).AddTo(disposables);
        windows.HasModal.Subscribe(_ => RefreshNavigation()).AddTo(disposables);

        ConnectLegacyUI();

        // 안드로이드 뒤로가기도 Escape로 들어온다.
        backAction = new InputAction("Back", InputActionType.Button, "<Keyboard>/escape");
        backAction.performed += HandleBack;
        backAction.Enable();
    }

    private void BindWindows(GameSession session, IReadOnlyList<SkillEntry> skills, PlayerController player)
    {
        // 쿨다운은 저장되지 않는 전투 상태라 세션이 아니라 캐릭터가 갖는다. Presenter에는 읽는 함수만 넘긴다.
        // 쿨다운은 시뮬레이션 시계 기준의 준비 시각이라, 지금 시각을 아는 캐릭터에게 비율을 묻는다.
        float CooldownRatio(int slot) => player.GetSkillCooldownRatio(slot);

        windows.Bind<SkillManageWindow, SkillManageArgs>((view, args, window) =>
            new SkillManagePresenter(view, session.Stats, skills, CooldownRatio, window));
    }

    private void Update()
    {
        if (windows == null) return;

        windows.Tick();
        inputBlocker.Tick();
    }

    private void OnDestroy()
    {
        if (windows == null) return;

        backAction.performed -= HandleBack;
        backAction.Disable();
        backAction.Dispose();

        DisconnectLegacyUI();
        windows.Dispose();
        disposables.Dispose();
    }

    private void HandleBack(InputAction.CallbackContext _)
    {
        if (inputBlocker.IsBlocked.Value) return;
        if (windows.Back()) return;

        uiManager.Back();
    }

    // 모달과 입력 차단은 터치만 막아서는 부족하다. 키보드 이동/Submit이 밑의 버튼을 누를 수 있다.
    private void RefreshNavigation()
    {
        if (EventSystem.current == null) return;

        EventSystem.current.sendNavigationEvents = !inputBlocker.IsBlocked.Value && !windows.HasModal.Value;
    }

    // ── 이관 기간 연결 ──────────────────────────────────────

    private void ConnectLegacyUI()
    {
        skillSlotBar.SkillManageRequested += OpenSkillManage;
        uiManager.OnTabChanged += HandleTabChanged;
    }

    private void DisconnectLegacyUI()
    {
        if (skillSlotBar != null) skillSlotBar.SkillManageRequested -= OpenSkillManage;
        if (uiManager != null) uiManager.OnTabChanged -= HandleTabChanged;
    }

    private void OpenSkillManage() => windows.Open<SkillManageWindow>(new SkillManageArgs());

    // 탭은 Exclusive(스택을 비우고 바닥이 됨)다. 탭을 WindowManager로 옮기면 이 연결은 사라진다.
    private void HandleTabChanged(UITabType _) => windows.CloseAll();

    private bool ValidateReferences()
    {
        bool ok = true;
        if (registry == null) { Debug.LogError("[UIRoot] registry(WindowRegistry)가 연결되지 않았습니다."); ok = false; }
        if (fadeLayer == null) { Debug.LogError("[UIRoot] fadeLayer가 연결되지 않았습니다."); ok = false; }
        if (fadeController == null) { Debug.LogError("[UIRoot] fadeController가 연결되지 않았습니다."); ok = false; }
        if (uiManager == null) { Debug.LogError("[UIRoot] uiManager가 연결되지 않았습니다."); ok = false; }
        if (skillSlotBar == null) { Debug.LogError("[UIRoot] skillSlotBar가 연결되지 않았습니다."); ok = false; }
        return ok;
    }
}
