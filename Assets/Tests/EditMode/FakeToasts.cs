using System.Collections.Generic;

// Presenter 테스트용: 보인 토스트 글을 순서대로 모은다.
public sealed class FakeToasts : IToastService
{
    public readonly List<string> Shown = new List<string>();

    public void Show(string message) => Shown.Add(message);
}
