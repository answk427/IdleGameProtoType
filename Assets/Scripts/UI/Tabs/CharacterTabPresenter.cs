using System;
using System.Globalization;
using UnityEngine;

// 캐릭터 탭의 로직. uGUI, GameManager를 모른다. 진행도의 상태를 구독해서 그리고, 버튼은 명령으로 바꾼다.
// - "현재 → 다음" 값은 진행도의 미리보기(PlayerStats.PreviewUpgrade)로 그린다. 실제 능력치와 같은 계산이라
//   능력치 공식이 바뀌어도 화면이 따로 틀리지 않는다.
// - 진행도를 통째로 바꿔도(GameSession.Apply) 구독한 상태가 바뀌므로 화면이 따라온다.
public sealed class CharacterTabPresenter : IWindowPresenter
{
    private readonly ICharacterTabView view;
    private readonly PlayerStats stats;
    private readonly IReadOnlyReactiveProperty<GameNumber> gold;
    private readonly Func<UpgradeStatType, UpgradeResult> checkUpgrade;
    private readonly Func<UpgradeStatType, UpgradeResult> tryUpgrade;
    private readonly IToastService toasts;
    private readonly CompositeDisposable disposables = new CompositeDisposable();

    // checkUpgrade: 지금 살 수 있는가(GameSession.CheckUpgrade, 바꾸지 않음). 줄의 "가능" 모양과 탭의 레드닷이 같은 규칙을 쓴다.
    // tryUpgrade: 골드 차감 + 업그레이드(GameSession.TryUpgrade). 진행도의 규칙이라 Presenter는 결과만 받는다.
    // toasts: 명령이 실패한 이유를 알린다.
    public CharacterTabPresenter(
        ICharacterTabView view,
        PlayerStats stats,
        IReadOnlyReactiveProperty<GameNumber> gold,
        Func<UpgradeStatType, UpgradeResult> checkUpgrade,
        Func<UpgradeStatType, UpgradeResult> tryUpgrade,
        IToastService toasts)
    {
        this.view = view;
        this.stats = stats;
        this.gold = gold;
        this.checkUpgrade = checkUpgrade ?? throw new ArgumentNullException(nameof(checkUpgrade));
        this.tryUpgrade = tryUpgrade;
        this.toasts = toasts ?? throw new ArgumentNullException(nameof(toasts));

        stats.Level.Subscribe(_ => RenderHeader()).AddTo(disposables);
        stats.Exp.Subscribe(_ => RenderHeader()).AddTo(disposables);
        stats.CanLevelUp.Subscribe(view.SetLevelUpAvailable).AddTo(disposables);

        // 줄의 값: 능력치(레벨·업그레이드 단계에서 나옴), 비용·최대 여부(업그레이드 단계), 살 수 있는지(골드).
        // 공격 간격이 하한에 닿으면 단계가 올라도 능력치는 그대로라 업그레이드 단계도 따로 구독한다.
        stats.CombatStats.Subscribe(_ => RenderRows()).AddTo(disposables);
        gold.Subscribe(_ => RenderRows()).AddTo(disposables);
        foreach (UpgradeStatType type in view.UpgradeRows)
            stats.UpgradeLevel(type).Subscribe(_ => RenderRows()).AddTo(disposables);

        view.LevelUpTapped += HandleLevelUpTapped;
        view.UpgradeTapped += HandleUpgradeTapped;
    }

    public void Tick() { }

    public void Dispose()
    {
        view.LevelUpTapped -= HandleLevelUpTapped;
        view.UpgradeTapped -= HandleUpgradeTapped;
        disposables.Dispose();
    }

    // ── 표시 문자열(현지화가 붙을 자리, UI_DESIGN 9장) ─────────

    public static string FormatLevel(int level) => $"Lv. {level}";

    public static string FormatExp(int exp, int required) =>
        $"EXP {NumberFormatUtility.Format(exp)} / {NumberFormatUtility.Format(required)}";

    public static string FormatExpPercent(int exp, int required)
    {
        double percent = required > 0 ? (double)exp / required * 100d : 0d;
        return percent.ToString("0.00", CultureInfo.InvariantCulture) + "%";
    }

    public static string FormatUpgradeLevel(int upgradeLevel) => $"Lv.{upgradeLevel + 1}";

    // 업그레이드가 올리는 능력치 하나. HP·공격력은 커지는 수라 단위를 줄여 보이고(1.2K),
    // 공격 간격은 작을수록 강해서 그대로 보이면 직관과 반대로 읽히므로 초당 공격 횟수로 바꿔 보인다.
    public static string FormatStat(UpgradeStatType type, PlayerCombatStats combat)
    {
        switch (type)
        {
            case UpgradeStatType.Hp: return NumberFormatUtility.Format(combat.MaxHp);
            case UpgradeStatType.Attack: return NumberFormatUtility.Format(combat.AttackDamage);
            case UpgradeStatType.AttackSpeed:
                return (1d / combat.AttackInterval).ToString("0.00", CultureInfo.InvariantCulture);
            default:
                Debug.LogError($"[CharacterTabPresenter] 표시 방법이 없는 업그레이드: {type}");
                return "-";
        }
    }

    // ── 그리기 ──────────────────────────────────────────────

    private void RenderHeader()
    {
        int exp = stats.Exp.Value;
        int required = stats.RequiredExp.Value;

        view.SetLevel(FormatLevel(stats.Level.Value));
        view.SetExp(required > 0 ? Mathf.Min(1f, (float)exp / required) : 0f, FormatExp(exp, required), FormatExpPercent(exp, required));
    }

    private void RenderRows()
    {
        PlayerCombatStats now = stats.CombatStats.Value;

        foreach (UpgradeStatType type in view.UpgradeRows)
        {
            bool isMaxed = stats.IsUpgradeMaxed(type);
            GameNumber cost = stats.GetNextUpgradeCost(type);

            string value = isMaxed
                ? FormatStat(type, now)
                : $"{FormatStat(type, now)} → {FormatStat(type, stats.PreviewUpgrade(type))}";

            view.SetUpgradeRow(type, new UpgradeRowModel(
                FormatUpgradeLevel(stats.UpgradeLevel(type).Value),
                value,
                isMaxed ? "MAX" : NumberFormatUtility.Format(cost),
                checkUpgrade(type) == UpgradeResult.Success));
        }
    }

    // 명령이 실패한 이유. 성공이면 null(화면은 바뀐 상태를 구독해 따라오므로 따로 알리지 않는다).
    public static string FormatUpgradeFailure(UpgradeResult result)
    {
        switch (result)
        {
            case UpgradeResult.Success: return null;
            case UpgradeResult.NotEnoughGold: return "골드가 부족합니다";
            case UpgradeResult.MaxLevel: return "최대 단계입니다";
            default: return result.ToString();
        }
    }

    public static string FormatLevelUpFailure(LevelUpResult result)
    {
        switch (result)
        {
            case LevelUpResult.Success: return null;
            case LevelUpResult.NotEnoughExp: return "경험치가 부족합니다";
            case LevelUpResult.MaxLevel: return "최대 레벨입니다";
            default: return result.ToString();
        }
    }

    // ── 입력 ────────────────────────────────────────────────
    // 판정은 진행도가 하고(10-1), 실패하면 이유를 토스트로 알린다. 화면은 바뀐 상태를 구독해 따라온다.

    private void HandleLevelUpTapped() => ShowFailure(FormatLevelUpFailure(stats.TryLevelUp()));

    private void HandleUpgradeTapped(UpgradeStatType type) => ShowFailure(FormatUpgradeFailure(tryUpgrade(type)));

    private void ShowFailure(string reason)
    {
        if (reason != null) toasts.Show(reason);
    }
}
