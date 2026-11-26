using System;

// Presenter가 아는 세이브 오류 창의 전부(docs/UI_DESIGN.md 5-7). 표시 문자열은 Presenter가 만든다.
public interface ISaveErrorView
{
    event Action QuitTapped;

    // reason: 작은 글씨 한 줄(문의·디버깅용).
    void SetMessage(string title, string body, string reason);

    void SetQuitLabel(string label);
}
