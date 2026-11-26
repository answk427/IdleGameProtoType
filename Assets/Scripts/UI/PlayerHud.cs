using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 플레이어 체력/경험치/레벨/골드만 보여주는 상시 HUD.
public class PlayerHud : UIHud
{
    [Header("HP")]
    [SerializeField] private Image hpFillImage;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("EXP / Level")]
    [SerializeField] private Image expFillImage;
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("Gold")]
    [SerializeField] private TextMeshProUGUI goldText;

    private PlayerController player;
    private CompositeDisposable subscriptions;

    // 4-B의 B-2에서 View + Presenter로 바뀐다(docs/COMBAT_ARCHITECTURE.md 10-10). 지금은 구독만 반응형이다.
    protected override void OnShown()
    {
        if (player == null && GameManager.Instance != null)
        {
            player = GameManager.Instance.GetPlayer();
        }

        if (player == null)
        {
            Debug.LogError("[PlayerHud] Player를 찾을 수 없습니다.");
            return;
        }

        subscriptions?.Dispose();
        subscriptions = new CompositeDisposable();
        player.Hp.Subscribe(UpdateHpBar).AddTo(subscriptions);
        player.Stats.Exp.Subscribe(_ => UpdateExpBar()).AddTo(subscriptions);
        player.Stats.RequiredExp.Subscribe(_ => UpdateExpBar()).AddTo(subscriptions);
        player.Stats.Level.Subscribe(UpdateLevelText).AddTo(subscriptions);
        if (GameManager.Instance != null)
            GameManager.Instance.Wallet.Gold.Subscribe(UpdateGoldText).AddTo(subscriptions);
    }

    protected override void OnHidden()
    {
        subscriptions?.Dispose();
        subscriptions = null;
    }

    private void UpdateHpBar(HpState hp)
    {
        if (hpFillImage != null) hpFillImage.fillAmount = hp.Ratio;
        if (hpText != null) hpText.text = $"{NumberFormatUtility.Format(hp.Current)} / {NumberFormatUtility.Format(hp.Max)}";
    }

    private void UpdateExpBar()
    {
        // 레벨업이 버튼으로 수동 처리되면서 경험치가 필요량을 넘어선 채로 머무를 수 있어 클램프.
        int required = player.Stats.RequiredExp.Value;
        if (expFillImage != null && required > 0)
            expFillImage.fillAmount = Mathf.Min(1f, (float)player.Stats.Exp.Value / required);
    }

    private void UpdateLevelText(int level)
    {
        if (levelText != null) levelText.text = $"Lv.{level}";
    }

    private void UpdateGoldText(GameNumber gold)
    {
        if (goldText != null) goldText.text = NumberFormatUtility.Format(gold);
    }
}
