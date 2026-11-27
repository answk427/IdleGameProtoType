using System;

// 플레이어 HUD의 로직(docs/COMBAT_ARCHITECTURE.md 10-10, docs/UI_DESIGN.md 5-7). uGUI를 모른다(게임 쪽은 주입받은 상태와 명령만 안다).
// - 진행도(레벨, 경험치, 골드)와 플레이어 HP는 상태라 구독한다. 구독하는 순간 지금 값을 그리고, 진행도를 통째로 바꿔도 따라온다.
// - 보스전 남은 시간은 매 프레임 값이다(원인은 흐름의 보스 제한 시각). UIRoot가 Tick을 부른다. 글자는 보이는 초가 바뀔 때만 만든다.
// - 표시 문자열("Lv.12", "1.2K", "보스 0:27")은 여기서만 만든다(현지화 자리, docs/UI_DESIGN.md 9장).
public sealed class PlayerHudPresenter : IDisposable
{
    // 남은 시간이 이만큼 이하면 강조한다(초).
    public const int UrgentSeconds = 10;

    private readonly IPlayerHudView view;
    private readonly PlayerStats stats;
    private readonly Func<double> bossDeadline;
    private readonly Func<double> now;
    private readonly CompositeDisposable subscriptions = new CompositeDisposable();

    private bool timerShown;
    private int shownSeconds = -1;

    public PlayerHudPresenter(IPlayerHudView view, PlayerStats stats, GoldWallet wallet, IReadOnlyReactiveProperty<HpState> playerHp,
        Func<double> bossDeadline, Func<double> now)
    {
        this.view = view ?? throw new ArgumentNullException(nameof(view));
        this.stats = stats ?? throw new ArgumentNullException(nameof(stats));
        if (wallet == null) throw new ArgumentNullException(nameof(wallet));
        if (playerHp == null) throw new ArgumentNullException(nameof(playerHp));
        this.bossDeadline = bossDeadline ?? throw new ArgumentNullException(nameof(bossDeadline));
        this.now = now ?? throw new ArgumentNullException(nameof(now));

        view.SetBossTimer(false, string.Empty, false);
        playerHp.Subscribe(hp => view.SetHp(hp.Ratio, $"{NumberFormatUtility.Format(hp.Current)} / {NumberFormatUtility.Format(hp.Max)}"))
            .AddTo(subscriptions);
        stats.Level.Subscribe(level => view.SetLevel($"Lv.{level}")).AddTo(subscriptions);
        stats.Exp.Subscribe(_ => RenderExp()).AddTo(subscriptions);
        stats.RequiredExp.Subscribe(_ => RenderExp()).AddTo(subscriptions);
        stats.IsMaxLevel.Subscribe(_ => RenderExp()).AddTo(subscriptions);
        wallet.Gold.Subscribe(gold => view.SetGold(NumberFormatUtility.Format(gold))).AddTo(subscriptions);
        view.SetVisible(true);
    }

    // 경험치 막대: 0~1로 자른다. 레벨업이 수동이라 경험치가 필요량을 넘어 머물 수 있다. 최대 레벨이거나 필요량이 0 이하면 가득.
    public static float ExpRatio(int exp, int required, bool isMaxLevel)
    {
        if (isMaxLevel || required <= 0) return 1f;
        if (exp <= 0) return 0f;
        return Math.Min(1f, (float)exp / required);
    }

    // 남은 초(올림)를 "m:ss"로. 0.3초 남으면 0:01, 끝나면 0:00.
    public static string FormatBossTime(int seconds) => $"보스 {seconds / 60}:{seconds % 60:00}";

    private void RenderExp() => view.SetExp(ExpRatio(stats.Exp.Value, stats.RequiredExp.Value, stats.IsMaxLevel.Value));

    public void Tick()
    {
        double deadline = bossDeadline();
        if (double.IsPositiveInfinity(deadline) || double.IsNaN(deadline))
        {
            if (!timerShown) return;
            timerShown = false;
            shownSeconds = -1;
            view.SetBossTimer(false, string.Empty, false);
            return;
        }

        double remaining = Math.Max(0.0, deadline - now());
        int seconds = (int)Math.Min(int.MaxValue, Math.Ceiling(remaining));
        if (timerShown && seconds == shownSeconds) return;

        timerShown = true;
        shownSeconds = seconds;
        view.SetBossTimer(true, FormatBossTime(seconds), seconds <= UrgentSeconds);
    }

    public void Dispose() => subscriptions.Dispose();
}
