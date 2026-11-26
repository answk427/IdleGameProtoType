// 짧은 안내 한 줄(토스트, docs/UI_DESIGN.md 10-6). Presenter가 명령의 실패 이유 같은 "방금 일어난 일"을 알릴 때 쓴다.
// 문장은 부르는 쪽(Presenter)이 만든다(현지화를 붙일 자리, 9장).
public interface IToastService
{
    void Show(string message);
}
