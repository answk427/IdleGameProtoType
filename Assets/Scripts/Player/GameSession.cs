using UnityEngine;

public enum UpgradeResult
{
    Success,
    MaxLevel,
    NotEnoughGold,
}

// 저장되는 진행도(레벨/경험치/업그레이드/스킬, 골드, 스테이지 번호)의 단일 소유자.
//
// 예전에는 진행도가 세 곳에 흩어져 있었다: 스탯은 캐릭터(PlayerController)의 필드, 골드는 GameManager,
// 스테이지는 StageManager. 세이브 파일은 하나인데 주인이 셋이라, 저장할 때마다 세 곳에서 모아야 했고
// 진행도의 수명이 캐릭터 GameObject에 묶여 있었다. 이제 GameInitializer가 이 객체를 한 번 만들고
// 필요한 쪽(캐릭터, GameManager, 이후 UI)에 넘겨준다.
//
// 저장되지 않는 전투 상태(현재 HP, 쿨다운, 타겟, FSM)는 여기 두지 않고 캐릭터가 갖는다.
// 기준은 "저장되는가"이다.
public class GameSession
{
    public PlayerStats Stats { get; } = new PlayerStats();
    public GoldWallet Wallet { get; } = new GoldWallet();

    // 저장된 스테이지 번호. 스테이지 안의 인카운터 진행도는 의도적으로 저장하지 않는다.
    public int CurrentStageNumber { get; private set; } = 1;

    public GameSession(PlayerStatDatabase statData, PlayerUpgradeConfig upgradeConfig)
    {
        Stats.Initialize(statData, upgradeConfig);
    }

    // 저장 데이터(또는 이후 서버가 돌려준 스냅샷)를 현재 세션에 적용한다.
    // 객체를 새로 만들지 않고 값만 덮어쓴다 — 이 세션과 그 안의 Stats/Wallet을 참조하는 쪽이
    // 옛 객체를 붙잡고 있는 일이 없게 하기 위해서다. 세이브 로드와 롤백이 같은 경로를 쓴다.
    public void Apply(PlayerSaveData data)
    {
        if (data == null) data = new PlayerSaveData();

        Stats.LoadSave(data.stats);
        Wallet.LoadGold(GameNumber.FromDouble(data.gold));
        CurrentStageNumber = Mathf.Max(1, data.currentStageNumber);
    }

    // 저장 데이터 조립은 이 한 곳에서만 한다.
    public PlayerSaveData ToSaveData()
    {
        return new PlayerSaveData
        {
            stats = Stats.GetSaveData(),
            gold = Wallet.Gold.ToDouble(),
            currentStageNumber = CurrentStageNumber,
        };
    }

    public void SetCurrentStageNumber(int stageNumber)
    {
        CurrentStageNumber = Mathf.Max(1, stageNumber);
    }

    // 업그레이드 규칙(골드 차감 + 스탯 증가)은 게임 규칙이라 캐릭터가 아니라 진행도의 주인이 판정한다.
    // 결과에 실패 이유를 담아 UI가 "최대 단계"와 "골드 부족"을 구분해서 안내할 수 있게 한다.
    public UpgradeResult TryUpgrade(UpgradeStatType type)
    {
        if (Stats.IsUpgradeMaxed(type)) return UpgradeResult.MaxLevel;
        if (!Wallet.TrySpendGold(Stats.GetNextUpgradeCost(type))) return UpgradeResult.NotEnoughGold;

        Stats.Upgrade(type);
        return UpgradeResult.Success;
    }
}
