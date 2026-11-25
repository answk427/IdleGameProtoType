using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 개발자 도구의 진입점(docs/DEV_TOOLS.md).
//
// 에디터와 개발 빌드에만 있다. 이 어셈블리(IdleGameProtoType.DevTools)는 define constraint가
// `UNITY_EDITOR || DEVELOPMENT_BUILD`라 릴리스 빌드에서는 컴파일되지 않는다(T1).
// 게임 코드는 이 클래스를 모른다. 씬이 열리면 스스로 생기고, GameInitializer가 조립을 끝냈다는 신호(Initialized)에 붙는다(T2).
//
// 열기: 화면의 DEV 버튼, 키보드 `(에디터). 닫기: 닫기 버튼, Escape(안드로이드 뒤로가기).
// 열려 있는 동안 게임 UI 입력은 InputBlocker로 막는다. IMGUI와 uGUI는 같은 터치를 둘 다 받기 때문이다.
public sealed class DevTools : MonoBehaviour
{
    // 코드에서 같은 명령을 부르는 입구(에디터의 Pipeline script eval 등). 게임이 시작되기 전이나 게임 씬이 아니면 null.
    public static DevCommands Commands { get; private set; }

    private GameInitializer initializer;
    private DevPanel panel;
    private HitboxOverlay overlay;
    private IDisposable inputLease;
    private bool isOpen;

    // 도메인 리로드를 끈 플레이에서도 이전 플레이의 명령이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Commands = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GameInitializer found = FindAnyObjectByType<GameInitializer>();
        if (found == null) return; // 게임 씬이 아니다

        new GameObject("DevTools").AddComponent<DevTools>().Attach(found);
    }

    private void Attach(GameInitializer target)
    {
        initializer = target;
        if (target.IsInitialized) HandleInitialized(target);
        else target.Initialized += HandleInitialized;
    }

    private void HandleInitialized(GameInitializer target)
    {
        target.Initialized -= HandleInitialized;

        Commands = new DevCommands(target.Session, target.GameManager, target.CombatSimulation.Director, new DevBackupStore(SaveProfile.SaveDirectory));
        overlay = new HitboxOverlay(target.CombatSimulation.World);
        panel = new DevPanel(Commands, overlay);
    }

    private void Update()
    {
        if (panel == null) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.backquoteKey.wasPressedThisFrame) SetOpen(!isOpen);
        else if (isOpen && keyboard.escapeKey.wasPressedThisFrame) SetOpen(false);
    }

    private void OnGUI()
    {
        if (panel == null) return;

        overlay.Draw(Camera.main, DevPanel.Unit);

        if (!isOpen)
        {
            if (panel.DrawToggleButton()) SetOpen(true);
        }
        else if (!panel.DrawPanel())
        {
            SetOpen(false);
        }
    }

    private void SetOpen(bool open)
    {
        if (isOpen == open) return;
        isOpen = open;

        if (open)
        {
            panel.OnOpened();
            // 사용자가 닫을 때까지 쥐는 차단이라 오래 걸려도 경고하지 않는다. 스피너도 필요 없다.
            inputLease = initializer.UIRoot.InputBlocker?.Acquire("개발자 도구 패널", showSpinner: false, warnIfLong: false);
        }
        else
        {
            ReleaseInput();
        }
    }

    private void ReleaseInput()
    {
        inputLease?.Dispose();
        inputLease = null;
    }

    private void OnDisable()
    {
        isOpen = false;
        ReleaseInput();
    }

    private void OnDestroy()
    {
        if (initializer != null) initializer.Initialized -= HandleInitialized;
        Commands = null;
    }
}
