// Presenter가 아는 스테이지 안내의 전부(docs/UI_DESIGN.md 5-7). 표시 문자열은 Presenter가 만든다.
public interface IStageAnnouncementView
{
    // 글을 바꾸고 처음부터 재생한다(떠 있는 중이면 다시).
    void Play(string text);

    void HideNow();
}
