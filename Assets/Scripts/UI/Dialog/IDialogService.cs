using UnityEngine;

// 확인창(docs/UI_DESIGN.md 6-3, 10-6):  bool ok = await dialogs.Confirm(new ConfirmArgs(제목, 본문));
// Presenter는 WindowManager 대신 이것을 받는다(가짜로 바꿔 시험할 수 있다).
public interface IDialogService
{
    // 확인을 누르면 true. 취소, 뒤로가기, 바깥 터치, 열지 못함(이미 떠 있음, 실패, 취소됨)은 false.
    Awaitable<bool> Confirm(ConfirmArgs args);
}
