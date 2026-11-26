// ToastService가 아는 토스트 View의 전부. 나타나고 사라지는 연출은 View(프리팹)가 한다.
public interface IToastView
{
    // 마지막으로 보인 때부터 사라질 때까지(초, 화면 시간). 연출이라 프리팹이 갖는다.
    float DisplaySeconds { get; }

    // 글을 바꾸고 나타난다. 이미 보이는 중이면 글을 바꾸고 다시 나타나는 연출을 한다(연타한 것을 알 수 있게).
    void Show(string message);

    void Hide();
}
