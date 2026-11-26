using System;
using UnityEngine;

// 뒤로가기 한 번의 처리(docs/UI_DESIGN.md 6-4, 10-6). uGUI·Input System을 모른다(UIRoot가 키를 받아 부른다).
// 1. 입력이 막혀 있으면 무시한다(창 전환, 화면 페이드 중).
// 2. 창이 있으면 맨 위 창이 처리한다(WindowManager.Back). 여는 중인 창은 먹기만 한다.
// 3. 창이 하나도 없으면 종료 확인창을 띄우고, 확인하면 종료한다.
// 종료 확인창이 떠 있을 때의 뒤로가기는 2번이 그 창을 닫는다(확인창도 스택의 창이다). 따로 짤 것이 없다.
// 세이브 오류 창(System, 뒤로가기로 닫히지 않음)이 떠 있으면 2번이 먹으므로 종료 확인창은 뜨지 않는다.
public sealed class BackNavigator
{
    // 표시 문자열(현지화를 붙일 자리, 9장).
    public const string QuitTitle = "게임 종료";
    public const string QuitMessage = "게임을 종료할까요?";
    public const string QuitConfirmLabel = "종료";
    public const string QuitCancelLabel = "취소";

    private readonly Func<bool> isInputBlocked;
    private readonly Func<bool> backInWindows;
    private readonly IDialogService dialogs;
    private readonly Action quit;

    // backInWindows: 맨 위 창이 처리하면 true, 창이 하나도 없으면 false(WindowManager.Back).
    // quit: 빌드는 Application.Quit(종료 저장은 OnApplicationQuit), 에디터는 플레이 종료.
    public BackNavigator(Func<bool> isInputBlocked, Func<bool> backInWindows, IDialogService dialogs, Action quit)
    {
        this.isInputBlocked = isInputBlocked ?? throw new ArgumentNullException(nameof(isInputBlocked));
        this.backInWindows = backInWindows ?? throw new ArgumentNullException(nameof(backInWindows));
        this.dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        this.quit = quit ?? throw new ArgumentNullException(nameof(quit));
    }

    public void HandleBack()
    {
        if (isInputBlocked()) return;
        if (backInWindows()) return;

        AskQuit();
    }

    // 예외는 여기서 로그로 바꾸므로 async void로 시작해도 새지 않는다.
    private async void AskQuit()
    {
        bool confirmed;
        try
        {
            confirmed = await dialogs.Confirm(new ConfirmArgs(QuitTitle, QuitMessage, QuitConfirmLabel, QuitCancelLabel));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return;
        }

        if (confirmed) quit();
    }
}
