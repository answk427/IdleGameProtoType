using System;

// Presenter가 아는 보스 도전 버튼의 전부(docs/UI_DESIGN.md 5-7).
public interface IBossChallengeView
{
    event Action Clicked;

    void SetVisible(bool visible);
}
