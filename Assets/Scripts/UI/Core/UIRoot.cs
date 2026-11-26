using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 화면 UI의 진입점이자 조립 지점 (docs/UI_DESIGN.md 3장). 준비는 두 단계다:
// - Prepare: 창 시스템(정렬 구분, 입력 차단, WindowManager, 뒤로가기, 토스트, 확인창)과 세션이 필요 없는 창
//   (세이브 오류 창, 확인창).
//   GameInitializer가 참조 검사 직후 항상 부른다. 그래서 창 시스템은 시작의 성공과 실패가 같은 코드로 준비된다.
// - BindGame: 게임이 시작될 때만. 세션·플레이어·흐름이 필요한 것(HUD Presenter, 탭과 스킬 창, 페이드).
// 시작에 실패하면(세이브를 읽지 못함 등) BindGame 대신 ShowSaveError를 부른다. HUD 위젯은 Presenter가 없으면 숨어 있거나
// (값을 보이는 위젯) 아무 일도 하지 않는다(탭바).
//
// 창(탭 내용, 팝업, 치명 오류)은 전부 WindowManager가 열고 닫는다. 씬에 배치된 것은 HUD 위젯뿐이다.
// 그리는 순서는 정렬 구분(UISortBand)이다. 씬의 HUD Canvas에도 여기서 구분을 넣는다:
// HUD·탭바(StaticCanvas) = Hud, 데미지 숫자(DynamicCanvas) = Fx. 창(Window, Popup), 페이드(System)는 그 위다.
[RequireComponent(typeof(Canvas))]
public class UIRoot : MonoBehaviour
{
    [SerializeField] private WindowRegistry registry;

    [Header("System 구분")]
    [SerializeField] private Canvas fadeLayer;
    [SerializeField] private FadeController fadeController;

    [Header("토스트 (Toast 구분, 스택 밖)")]
    [SerializeField] private ToastView toastPrefab;

    [Header("소리 (UIButton 클릭)")]
    [SerializeField] private UISoundSet soundSet;

    [Header("HUD 위젯 (View, Presenter는 여기서 만든다)")]
    [SerializeField] private PlayerHud playerHud;
    [SerializeField] private BossChallengeButton bossChallengeButton;
    [SerializeField] private StageAnnouncement stageAnnouncement;
    [SerializeField] private TabBar tabBar;

    [Header("정렬 구분 (씬의 HUD Canvas)")]
    [Tooltip("HUD, 보스 버튼, 스테이지 안내, 탭바가 있는 Canvas → Hud 구분")]
    [SerializeField] private Canvas hudCanvas;
    [Tooltip("데미지 숫자가 있는 Canvas → Fx 구분(HUD 아래, GraphicRaycaster 없음)")]
    [SerializeField] private Canvas fxCanvas;

    private readonly CompositeDisposable disposables = new CompositeDisposable();

    private PlayerHudPresenter hudPresenter;
    private WindowManager windows;
    private InputBlocker inputBlocker;
    private ToastService toasts;
    private DialogService dialogs;
    private BackNavigator backNavigator;
    private InputAction backAction;
    private bool isGameBound;

    // Prepare 뒤에만 있다(게임 시작에 실패해도 있다). 창이 아닌 화면(개발 도구 패널)도 같은 입력 차단을 쓴다.
    public InputBlocker InputBlocker => inputBlocker;

    // 지금 UI 상태(창 스택, 딤, 입력 차단 이유, 토스트)를 사본에 채운다. 개발자 도구의 UI 상태 표시가 읽는다.
    // Prepare 전이면 비어 있다.
    public void Describe(UIDebugSnapshot into)
    {
        into.Clear();
        if (windows == null) return;

        windows.Describe(into);
        inputBlocker.Describe(into);
        into.Toast = toasts.Current;
    }

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
        toasts = CreateToasts();
        dialogs = new DialogService(windows);
        backNavigator = new BackNavigator(() => inputBlocker.IsBlocked.Value, windows.Back, dialogs, QuitGame);
        CreateSoundPlayer().AddTo(disposables);

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
        windows.Bind<ConfirmDialogWindow, ConfirmArgs, bool>((view, args, window) => new ConfirmDialogPresenter(view, args, window));
    }

    // 토스트는 창이 아니라 스택 밖의 층 하나다(뒤로가기·AlreadyOpen·입력 차단과 무관, docs/UI_DESIGN.md 10-6).
    // 층에 GraphicRaycaster를 두지 않아 밑의 터치를 가로채지 않는다(Fx Canvas와 같은 규칙).
    private ToastService CreateToasts()
    {
        Canvas layer = UILayerFactory.CreateLayer("Toast", transform, (int)UISortBand.Toast);
        ToastView view = Instantiate(toastPrefab, layer.transform, false);
        return new ToastService(view, () => Time.unscaledTime);
    }

    // 버튼 클릭 소리를 내는 한 곳(UIButtonEvents를 구독한다). 2D 소리라 위치와 무관하다.
    private UISoundPlayer CreateSoundPlayer()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return new UISoundPlayer(soundSet.Get, source.PlayOneShot);
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

        windows.Bind<CharacterTabWindow, CharacterTabArgs>((view, args, window) =>
            new CharacterTabPresenter(view, session.Stats, session.Wallet.Gold, session.CheckUpgrade, session.TryUpgrade, toasts));

        windows.Bind<SkillTabWindow, SkillTabArgs>((view, args, window) =>
            new SkillTabPresenter(view, session.Stats, skills, CooldownRatio, player.RequestSkillSlot, OpenSkillManage));

        windows.Bind<SkillManageWindow, SkillManageArgs>((view, args, window) =>
            new SkillManagePresenter(view, session.Stats, skills, CooldownRatio, window, toasts));
    }

    private void OpenSkillManage() => windows.Open<SkillManageWindow>(new SkillManageArgs());

    // 씬에 배치된 HUD 위젯. 창이 아니라 WindowManager를 거치지 않고, Presenter의 수명은 UIRoot와 같다.
    private void BindHudWidgets(GameSession session, PlayerController player, EncounterDirector flow)
    {
        // 보스전 남은 시간은 흐름의 제한 시각과 시뮬레이션 시계로 계산한다(매 프레임 값, Tick).
        hudPresenter = new PlayerHudPresenter(playerHud, session.Stats, session.Wallet, player.Hp,
            () => flow.BossDeadline, () => flow.Clock.Now).AddTo(disposables);
        new BossChallengePresenter(bossChallengeButton, flow).AddTo(disposables);
        new StageAnnouncementPresenter(stageAnnouncement, flow.CurrentStageNumber).AddTo(disposables);

        // 탭 = Exclusive 창. 여기 있는 탭(내용 창이 있는 탭)만 해금되면 열리고, 나머지는 "준비 중"이다.
        // 탭 창을 여는 함수는 탭바 Presenter만 받는다. 탭(UI) → 콘텐츠(게임)의 짝은 여기서 맺는다.
        // 레드닷 묶음(어느 탭에 어느 조건)은 화면 구성이라 UI가 정하고, 조건 하나하나는 게임 쪽 파생 상태다(5-8).
        var characterBadge = new DerivedProperty<bool>(
            () => session.Stats.CanLevelUp.Value || session.CanBuyUpgrade.Value,
            session.Stats.CanLevelUp, session.CanBuyUpgrade).AddTo(disposables);

        TabEntry Tab(GameContent content, Func<WindowHandle> open, IReadOnlyReactiveProperty<bool> badge) =>
            new TabEntry(open, session.Unlocks.IsUnlocked(content), () => session.Unlocks.ConditionOf(content), badge);

        var tabs = new Dictionary<UITabType, TabEntry>
        {
            [UITabType.Character] = Tab(GameContent.Character, () => windows.Open<CharacterTabWindow>(new CharacterTabArgs()), characterBadge),
            [UITabType.Skill] = Tab(GameContent.Skill, () => windows.Open<SkillTabWindow>(new SkillTabArgs()), session.Stats.HasLearnableSkill),
        };
        new TabBarPresenter(tabBar, tabs, () => windows.CloseBelow(UISortBand.System), toasts).AddTo(disposables);
    }

    private void Update()
    {
        if (windows == null) return;

        windows.Tick();
        inputBlocker.Tick();
        toasts.Tick();
        hudPresenter?.Tick(); // 게임이 시작되지 않으면 없다
    }

    private void OnDestroy()
    {
        if (windows == null) return;

        backAction.performed -= HandleBack;
        backAction.Disable();
        backAction.Dispose();

        // HUD Presenter(탭바 포함)를 먼저 버린다. 창을 닫을 때 나오는 알림에 파괴 중인 위젯이 반응하지 않게.
        disposables.Dispose();
        windows.Dispose();
    }

    // 입력 차단 중 무시 → 맨 위 창 → 창이 없으면 종료 확인창(BackNavigator).
    private void HandleBack(InputAction.CallbackContext _) => backNavigator.HandleBack();

    // 모달과 입력 차단은 터치만 막아서는 부족하다. 키보드 이동/Submit이 밑의 버튼을 누를 수 있다.
    private void RefreshNavigation()
    {
        if (EventSystem.current == null) return;

        EventSystem.current.sendNavigationEvents = !inputBlocker.IsBlocked.Value && !windows.HasModal.Value;
    }

    private bool ValidateReferences()
    {
        bool ok = true;
        if (registry == null) { Debug.LogError("[UIRoot] registry(WindowRegistry)가 연결되지 않았습니다."); ok = false; }
        if (fadeLayer == null) { Debug.LogError("[UIRoot] fadeLayer가 연결되지 않았습니다."); ok = false; }
        if (fadeController == null) { Debug.LogError("[UIRoot] fadeController가 연결되지 않았습니다."); ok = false; }
        if (toastPrefab == null) { Debug.LogError("[UIRoot] toastPrefab(ToastView)이 연결되지 않았습니다."); ok = false; }
        if (soundSet == null) { Debug.LogError("[UIRoot] soundSet(UISoundSet)이 연결되지 않았습니다."); ok = false; }
        if (playerHud == null) { Debug.LogError("[UIRoot] playerHud가 연결되지 않았습니다."); ok = false; }
        if (hudCanvas == null) { Debug.LogError("[UIRoot] hudCanvas(StaticCanvas)가 연결되지 않았습니다."); ok = false; }
        if (fxCanvas == null) { Debug.LogError("[UIRoot] fxCanvas(DynamicCanvas)가 연결되지 않았습니다."); ok = false; }
        if (bossChallengeButton == null) { Debug.LogError("[UIRoot] bossChallengeButton이 연결되지 않았습니다."); ok = false; }
        if (stageAnnouncement == null) { Debug.LogError("[UIRoot] stageAnnouncement가 연결되지 않았습니다."); ok = false; }
        if (tabBar == null) { Debug.LogError("[UIRoot] tabBar가 연결되지 않았습니다."); ok = false; }
        return ok;
    }
}
