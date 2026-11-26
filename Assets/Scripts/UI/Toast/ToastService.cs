using System;

// 토스트의 규칙(docs/UI_DESIGN.md 10-6). uGUI를 모른다.
// - 한 번에 하나. 새 글이 오면 지금 글을 바꾸고, 마지막 Show부터 DisplaySeconds 뒤에 사라진다.
// - 대기열은 없다. 버튼을 연타하면 안내가 줄을 서서 늦게 나오기 때문이다(누른 것에 대한 답은 바로 보여야 한다).
// - 시간은 화면 시간(unscaled)이다. 게임 시간이 멈추거나 배속이어도 같은 시간 동안 보인다(6-5).
//
// 창이 아니다. 창이면 스택에 들어가 뒤로가기를 먼저 먹고, 같은 창은 AlreadyOpen으로 거절되며, 열 때 입력이 막힌다.
// 토스트는 화면 이동도 입력도 아니라서 UIRoot가 스택 밖의 층(UISortBand.Toast, 터치를 받지 않음)에 하나 둔다.
public sealed class ToastService : IToastService
{
    private readonly IToastView view;
    private readonly Func<float> now;

    private string current;
    private float hideAt;

    // now: 화면 시간(UIRoot는 Time.unscaledTime, 테스트는 가짜 시계).
    public ToastService(IToastView view, Func<float> now)
    {
        this.view = view ?? throw new ArgumentNullException(nameof(view));
        this.now = now ?? throw new ArgumentNullException(nameof(now));
    }

    // 지금 보이는 글. 없으면 null(디버그 오버레이용).
    public string Current => current;

    public void Show(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        current = message;
        hideAt = now() + Math.Max(0f, view.DisplaySeconds);
        view.Show(message);
    }

    // UIRoot가 매 프레임 부른다.
    public void Tick()
    {
        if (current == null || now() < hideAt) return;

        current = null;
        view.Hide();
    }
}
