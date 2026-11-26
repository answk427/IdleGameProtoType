using System.Collections.Generic;
using NUnit.Framework;

// 토스트 규칙(docs/UI_DESIGN.md 10-6): 한 번에 하나, 새 글은 지금 글을 바꾼다, 마지막 Show부터 DisplaySeconds 뒤에 사라진다.
// 대기열이 없어서 연타해도 안내가 밀리지 않는다. 시계는 가짜(화면 시간).
[TestFixture]
public class ToastServiceTests
{
    private sealed class FakeView : IToastView
    {
        public readonly List<string> Log = new List<string>();
        public float DisplaySeconds { get; set; } = 2f;

        public void Show(string message) => Log.Add("show:" + message);
        public void Hide() => Log.Add("hide");
    }

    private FakeView view;
    private float now;
    private ToastService toasts;

    [SetUp]
    public void SetUp()
    {
        view = new FakeView();
        now = 100f;
        toasts = new ToastService(view, () => now);
    }

    [Test]
    public void Show_ThenHidesExactlyAfterDisplaySeconds()
    {
        toasts.Show("A");
        Assert.AreEqual("A", toasts.Current);

        now = 101.999f;
        toasts.Tick();
        CollectionAssert.AreEqual(new[] { "show:A" }, view.Log, "시간 전에는 그대로");

        now = 102f;
        toasts.Tick();
        CollectionAssert.AreEqual(new[] { "show:A", "hide" }, view.Log);
        Assert.IsNull(toasts.Current);

        toasts.Tick();
        Assert.AreEqual(2, view.Log.Count, "한 번만 숨긴다");
    }

    [Test]
    public void NewMessage_ReplacesCurrent_AndRestartsTime_NoQueue()
    {
        toasts.Show("A");
        now = 101.5f;
        toasts.Show("B");

        now = 103f; // A 기준이면 이미 사라졌을 시각, B 기준으로는 아직
        toasts.Tick();
        CollectionAssert.AreEqual(new[] { "show:A", "show:B" }, view.Log);
        Assert.AreEqual("B", toasts.Current);

        now = 103.5f;
        toasts.Tick();
        CollectionAssert.AreEqual(new[] { "show:A", "show:B", "hide" }, view.Log, "A가 줄을 서서 다시 나오지 않는다");
    }

    // 연타: 같은 글이 계속 와도 쌓이지 않고, 마지막 탭부터 시간을 센다.
    [Test]
    public void SameMessageSpam_KeepsOneToast_UntilLastTapPlusDuration()
    {
        float lastShow = now;
        for (int i = 0; i < 100; i++)
        {
            lastShow = now;
            toasts.Show("골드가 부족합니다");
            now += 0.1f;
            toasts.Tick();
        }

        Assert.IsFalse(view.Log.Contains("hide"), "연타하는 동안 사라지지 않는다");

        now = lastShow + 2f;
        toasts.Tick();
        Assert.AreEqual("hide", view.Log[view.Log.Count - 1]);
    }

    [Test]
    public void EmptyMessage_IsIgnored()
    {
        toasts.Show(null);
        toasts.Show("");
        Assert.IsEmpty(view.Log);
        Assert.IsNull(toasts.Current);
    }

    // 극단값: 보이는 시간이 0이나 음수면 다음 Tick에 사라진다(멈추지 않는다).
    [Test]
    public void ZeroOrNegativeDuration_HidesOnNextTick()
    {
        view.DisplaySeconds = -5f;
        toasts.Show("A");
        toasts.Tick();
        CollectionAssert.AreEqual(new[] { "show:A", "hide" }, view.Log);
    }
}
