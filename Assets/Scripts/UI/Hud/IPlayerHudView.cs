// Presenter가 아는 플레이어 HUD의 전부(docs/UI_DESIGN.md 5-7). 표시 문자열은 Presenter가 만든다.
public interface IPlayerHudView
{
    void SetVisible(bool visible);

    // 비율은 0~1
    void SetHp(float ratio, string text);
    void SetExp(float ratio);
    void SetLevel(string text);
    void SetGold(string text);

    // 보스전 남은 시간. urgent면 강조(곧 끝남).
    void SetBossTimer(bool visible, string text, bool urgent);
}
