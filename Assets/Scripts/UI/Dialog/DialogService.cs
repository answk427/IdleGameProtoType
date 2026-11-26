using System;
using UnityEngine;

// 확인창을 여는 곳. 창의 결과 다섯 가지(WindowStatus)를 "확인했는가" 하나로 바꾸는 유일한 곳이다.
// 부르는 쪽은 상태를 고를 것이 없다: 확인만 true, 나머지는 전부 false.
//
// 확인창은 한 번에 하나다(WindowManager는 같은 창을 둘 열지 않는다). 모달이라 사용자 입력으로는 겹치지 않고,
// 겹치면(코드가 연달아 물음) 나중 것은 false가 되고 경고를 남긴다.
// 창의 Bind는 다른 창처럼 UIRoot가 한다(세션이 필요 없어 Prepare에서).
public sealed class DialogService : IDialogService
{
    private readonly WindowManager windows;

    public DialogService(WindowManager windows)
    {
        this.windows = windows ?? throw new ArgumentNullException(nameof(windows));
    }

    public async Awaitable<bool> Confirm(ConfirmArgs args)
    {
        WindowResult<bool> result = await windows.Open(args).Closed;
        return ToAnswer(result);
    }

    public static bool ToAnswer(WindowResult<bool> result)
    {
        switch (result.Status)
        {
            case WindowStatus.Closed:
                return result.Value;
            case WindowStatus.AlreadyOpen:
                Debug.LogWarning("[DialogService] 확인창이 이미 떠 있어 이번 질문은 '아니오'로 처리합니다.");
                return false;
            default: // Dismissed(뒤로가기, 바깥 터치, 화면 이동), Failed(로그 남음), Cancelled(UIRoot 파괴)
                return false;
        }
    }
}
