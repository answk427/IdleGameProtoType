using System;
using System.Collections.Generic;
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
    private static readonly UpgradeStatType[] UpgradeTypes = (UpgradeStatType[])Enum.GetValues(typeof(UpgradeStatType));

    private readonly ReactiveProperty<int> currentStageNumber = new ReactiveProperty<int>(1);
    private readonly DerivedProperty<bool> canBuyUpgrade;

    public PlayerStats Stats { get; } = new PlayerStats();
    public GoldWallet Wallet { get; } = new GoldWallet();

    // 저장된 스테이지 번호. 스테이지 안의 인카운터 진행도는 의도적으로 저장하지 않는다.
    // 상태라서 해금(StageReached)이 구독한다. 흐름의 지금 스테이지(EncounterDirector.CurrentStageNumber)와 같은 이름이지만,
    // 이것은 진행도(저장되는 값)이고 저것은 흐름의 상태(시작 전 0)다.
    public IReadOnlyReactiveProperty<int> CurrentStageNumber => currentStageNumber;

    // 콘텐츠 해금(파생 상태, 저장하지 않는다).
    public ContentUnlocks Unlocks { get; }

    // 지금 골드로 살 수 있는 업그레이드가 있다(레드닷 조건). 판정은 TryUpgrade와 같은 CheckUpgrade다.
    public IReadOnlyReactiveProperty<bool> CanBuyUpgrade => canBuyUpgrade;

    // skills: 스킬 표, unlockRules: 콘텐츠 해금 조건 표. 시험에서 쓰지 않으면 비워도 된다
    // (스킬이 없으면 배울 스킬도 없고, 해금 표가 없으면 묻는 콘텐츠가 잠기며 에러를 남긴다).
    public GameSession(PlayerStatDatabase statData, PlayerUpgradeConfig upgradeConfig,
        IReadOnlyList<SkillEntry> skills = null, ContentUnlockDatabase unlockRules = null)
    {
        Stats.Initialize(statData, upgradeConfig, skills);
        Unlocks = new ContentUnlocks(unlockRules, Stats.Level, currentStageNumber);

        var sources = new List<IChangeSignal> { Wallet.Gold };
        foreach (UpgradeStatType type in UpgradeTypes) sources.Add(Stats.UpgradeLevel(type));
        canBuyUpgrade = new DerivedProperty<bool>(ComputeCanBuyUpgrade, sources.ToArray());
    }

    // 에디터의 튜닝 덮어쓰기로 데이터가 바뀌면 파생 상태를 다시 계산한다.
    public void RecomputeFromData()
    {
        Stats.RecomputeFromData();
        canBuyUpgrade.Recompute();
        Unlocks.Recompute();
    }

    // 저장 데이터(또는 이후 서버가 돌려준 스냅샷)를 현재 세션에 적용한다.
    // 객체를 새로 만들지 않고 값만 덮어쓴다 — 이 세션과 그 안의 Stats/Wallet을 참조하는 쪽이
    // 옛 객체를 붙잡고 있는 일이 없게 하기 위해서다. 세이브 로드와 롤백이 같은 경로를 쓴다.
    public void Apply(PlayerSaveData data)
    {
        if (data == null) data = new PlayerSaveData();

        Stats.LoadSave(data.stats);
        Wallet.LoadGold(GameNumber.FromDouble(data.gold));
        currentStageNumber.Value = Mathf.Max(1, data.currentStageNumber);
    }

    // 저장 데이터 조립은 이 한 곳에서만 한다.
    public PlayerSaveData ToSaveData()
    {
        return new PlayerSaveData
        {
            stats = Stats.GetSaveData(),
            gold = Wallet.Gold.Value.ToDouble(),
            currentStageNumber = currentStageNumber.Value,
        };
    }

    public void SetCurrentStageNumber(int stageNumber)
    {
        currentStageNumber.Value = Mathf.Max(1, stageNumber);
    }

    // 업그레이드 규칙(골드 차감 + 스탯 증가)은 게임 규칙이라 캐릭터가 아니라 진행도의 주인이 판정한다.
    // 결과에 실패 이유를 담아 UI가 "최대 단계"와 "골드 부족"을 구분해서 안내할 수 있게 한다.
    public UpgradeResult TryUpgrade(UpgradeStatType type)
    {
        UpgradeResult check = CheckUpgrade(type);
        if (check != UpgradeResult.Success) return check;
        if (!Wallet.TrySpendGold(Stats.GetNextUpgradeCost(type))) return UpgradeResult.NotEnoughGold;

        Stats.Upgrade(type);
        return UpgradeResult.Success;
    }

    // 지금 이 업그레이드를 살 수 있는가(바꾸지 않고 판정만). TryUpgrade와 레드닷(CanBuyUpgrade)이 같은 규칙을 쓴다.
    public UpgradeResult CheckUpgrade(UpgradeStatType type)
    {
        if (Stats.IsUpgradeMaxed(type)) return UpgradeResult.MaxLevel;
        if (Wallet.Gold.Value < Stats.GetNextUpgradeCost(type)) return UpgradeResult.NotEnoughGold;
        return UpgradeResult.Success;
    }

    private bool ComputeCanBuyUpgrade()
    {
        foreach (UpgradeStatType type in UpgradeTypes)
        {
            if (CheckUpgrade(type) == UpgradeResult.Success) return true;
        }
        return false;
    }
}
