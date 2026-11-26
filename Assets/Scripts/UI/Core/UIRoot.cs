using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 새 화면 UI의 진입점이자 조립 지점 (docs/UI_DESIGN.md 3장). 준비는 두 단계다:
// - Prepare: 창 시스템(정렬 구분, 입력 차단, WindowManager, 뒤로가기)과 세션이 필요 없는 창(세이브 오류 창).
//   GameInitializer가 참조 검사 직후 항상 부른다. 그래서 창 시스템은 시작의 성공과 실패가 같은 코드로 준비된다.
// - BindGame: 게임이 시작될 때만. 세션·플레이어·흐름이 필요한 것(HUD Presenter, 스킬 창, 페이드, 탭 연결).
// 시작에 실패하면(세이브를 읽지 못함 등) BindGame 대신 ShowSaveError를 부른다. HUD 위젯은 Presenter가 없으면 숨어 있다.
//
// 이관 기간에는 기존 UIManager(HUD, 탭)와 공존한다. 규칙:
// - 화면 하나의 주인은 하나다. 지금 WindowManager의 창은 SkillManageWindow(Popup)와 SaveErrorWindow(System)다.
// - 두 시스템을 아는 곳은 여기뿐이다("이관 기간 연결"). 이관이 끝나면 그 부분만 지우면 된다.
// - 그리는 순서는 정렬 구분(UISortBand)이다. 기존 UIManager 쪽 Canvas도 여기서 구분을 넣는다:
//   HUD·탭(StaticCanvas) = Hud, 데미지 숫자(DynamicCanvas) = Fx. 창(Window), 페이드(System)는 그 위다.
[RequireComponent(typeof(Canvas))]
public class UIRoot : MonoBehaviour
{
    [SerializeField] private WindowRegistry registry;

    [Header("System 구분")]
    [SerializeField] private Canvas fadeLayer;
    [SerializeField] private FadeController fadeController;

    [Header("HUD 위젯 (View, Presenter는 여기서 만든다)")]
    [SerializeField] private PlayerHud playerHud;
    [SerializeField] private BossChallengeButton bossChallengeButton;
    [SerializeField] private StageAnnouncement stageAnnouncement;

    [Header("정렬 구분 (기존 UIManager 쪽 Canvas)")]
    [Tooltip("HUD, 보스 버튼, 스테이지 안내, 탭이 있는 Canvas → Hud 구분")]
    [SerializeField] private Canvas hudCanvas;
    [Tooltip("데미지 숫자가 있는 Canvas → Fx 구분(HUD 아래, GraphicRaycaster 없음)")]
    [SerializeField] private Canvas fxCanvas;

    [Header("이관 기간 연결 (기존 UIManager 쪽)")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private SkillSlotBar skillSlotBar;

    private readonly CompositeDisposable disposables = new CompositeDisposable();

    private PlayerHudPresenter hudPresenter;
    private WindowManager windows;
    private InputBlocker inputBlocker;
    private InputAction backAction;
    private bool isGameBound;

    // Prepare 뒤에만 있다(게임 시작에 실패해도 있다). 창이 아닌 화면(개발 도구 패널)도 같은 입력 차단을 쓴다.
    public InputBlocker InputBlocker => inputBlocker;

    // 창 시스템을 준비한다. 세션이 없어도 된다. 참조가 빠졌으면 false(에러 로그, 창을 띄울 수 없다).
    public bool Prepare()
    {
        if (windows != null) return true;
        if (!ValidateReferences()) return false;

        UISort.Apply(fadeLayer, UISort.FadeOrder);
        UISort.Apply(hudCanvas, (int)UISortBand.Hud);
        UISort.Apply(fxCanvas, (int)UISortBand.Fx);

        inputBlocker = new InputBlocker(transform);
        windows = new WindowManager(transform, registry, inputBlocker);

        BindSystemWindows();

        inputBlocker.IsBlocked.Subscribe(_ => RefreshNavigation()).AddTo(disposables);
        windows.HasModal.Subscribe(_ => RefreshNavigation()).AddTo(disposables);

        // 안드로이드 뒤로가기도 Escape로 들어온다.
        backAction = new InputAction("Back", InputActionType.Button, "<Keyboard>/escape");
        backAction.performed += HandleBack;
        backAction.Enable();
        return true;
    }

    // 게임이 시작될 때만 부른다(Prepare 뒤).
    public void BindGame(GameSession session, IReadOnlyList<SkillEntry> skills, PlayerController player, EncounterDirector flow)
    {
        if (isGameBound) return;
        if (windows == null)
        {
            Debug.LogError("[UIRoot] Prepare가 끝나지 않아 게임 UI를 연결하지 않습니다.");
            return;
        }
        isGameBound = true;

        fadeController.Initialize(inputBlocker, flow);
        BindGameWindows(session, skills, player);
        BindHudWidgets(session, player, flow);
        ConnectLegacyUI();
    }

    // 시작을 멈췄다는 안내(System 구분, 닫는 길 없음). 게임 종료 버튼만 있다.
    public void ShowSaveError(SaveErrorArgs args)
    {
        if (windows == null)
        {
            Debug.LogError("[UIRoot] Prepare가 끝나지 않아 세이브 오류 창을 띄울 수 없습니다.");
            return;
        }

        windows.Open<SaveErrorWindow>(args);
    }

    // 세션이 없어도 여는 창. 시작에 실패했을 때도 열 수 있어야 한다.
    private void BindSystemWindows()
    {
        windows.Bind<SaveErrorWindow, SaveErrorArgs>((view, args, _) => new SaveErrorPresenter(view, args, QuitGame));
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BindGameWindows(GameSession session, IReadOnlyList<SkillEntry> skills, PlayerController player)
    {
        // 쿨다운은 저장되지 않는 전투 상태라 세션이 아니라 캐릭터가 갖는다. Presenter에는 읽는 함수만 넘긴다.
        // 쿨다운은 시뮬레이션 시계 기준의 준비 시각이라, 지금 시각을 아는 캐릭터에게 비율을 묻는다.
        float CooldownRatio(int slot) => player.GetSkillCooldownRatio(slot);

        windows.Bind<SkillManageWindow, SkillManageArgs>((view, args, window) =>
            new SkillManagePresenter(view, session.Stats, skills, CooldownRatio, window));
    }

    // 씬에 배치된 HUD 위젯. 창이 아니라 WindowManager를 거치지 않고, Presenter의 수명은 UIRoot와 같다.
    private void BindHudWidgets(GameSession session, PlayerController player, EncounterDirector flow)
    {
        // 보스전 남은 시간은 흐름의 제한 시각과 시뮬레이션 시계로 계산한다(매 프레임 값, Tick).
        hudPresenter = new PlayerHudPresenter(playerHud, session.Stats, session.Wallet, player.Hp,
            () => flow.BossDeadline, () => flow.Clock.Now).AddTo(disposables);
        new BossChallengePresenter(bossChallengeButton, flow).AddTo(disposables);
        new StageAnnouncementPresenter(stageAnnouncement, flow.CurrentStageNumber).AddTo(disposables);
    }

    private void Update()
    {
        if (windows == null) return;

        windows.Tick();
        inputBlocker.Tick();
        hudPresenter?.Tick(); // 게임이 시작되지 않으면 없다
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

    // 탭은 Exclusive(스택을 비우고 바닥이 됨)다. System 구분(세이브 오류 등)은 탭 전환으로 닫지 않는다.
    // 탭을 WindowManager로 옮기면 이 연결은 사라지고, Exclusive 창이 열릴 때 같은 함수를 쓴다.
    private void HandleTabChanged(UITabType _) => windows.CloseBelow(UISortBand.System);

    private bool ValidateReferences()
    {
        bool ok = true;
        if (registry == null) { Debug.LogError("[UIRoot] registry(WindowRegistry)가 연결되지 않았습니다."); ok = false; }
        if (fadeLayer == null) { Debug.LogError("[UIRoot] fadeLayer가 연결되지 않았습니다."); ok = false; }
        if (fadeController == null) { Debug.LogError("[UIRoot] fadeController가 연결되지 않았습니다."); ok = false; }
        if (playerHud == null) { Debug.LogError("[UIRoot] playerHud가 연결되지 않았습니다."); ok = false; }
        if (hudCanvas == null) { Debug.LogError("[UIRoot] hudCanvas(StaticCanvas)가 연결되지 않았습니다."); ok = false; }
        if (fxCanvas == null) { Debug.LogError("[UIRoot] fxCanvas(DynamicCanvas)가 연결되지 않았습니다."); ok = false; }
        if (bossChallengeButton == null) { Debug.LogError("[UIRoot] bossChallengeButton이 연결되지 않았습니다."); ok = false; }
        if (stageAnnouncement == null) { Debug.LogError("[UIRoot] stageAnnouncement가 연결되지 않았습니다."); ok = false; }
        if (uiManager == null) { Debug.LogError("[UIRoot] uiManager가 연결되지 않았습니다."); ok = false; }
        if (skillSlotBar == null) { Debug.LogError("[UIRoot] skillSlotBar가 연결되지 않았습니다."); ok = false; }
        return ok;
    }
}
