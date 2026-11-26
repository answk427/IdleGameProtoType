using System;
using System.Collections.Generic;

// Presenter가 아는 캐릭터 탭의 전부. 표시 문자열은 Presenter가 만들어서 넘긴다.
public interface ICharacterTabView
{
    event Action LevelUpTapped;
    event Action<UpgradeStatType> UpgradeTapped;

    // 목록에 있는 업그레이드(프리팹에 정의한 순서). 이름과 아이콘은 연출이라 View가 갖는다.
    IReadOnlyList<UpgradeStatType> UpgradeRows { get; }

    void SetLevel(string text);
    void SetExp(float fillRatio, string text, string percentText);
    void SetLevelUpAvailable(bool available);
    void SetUpgradeRow(UpgradeStatType type, UpgradeRowModel row);
}

// 업그레이드 목록 한 줄의 표시값.
public readonly struct UpgradeRowModel
{
    public string Level { get; }
    public string Value { get; }
    public string Cost { get; }
    public bool CanUpgrade { get; }

    public UpgradeRowModel(string level, string value, string cost, bool canUpgrade)
    {
        Level = level;
        Value = value;
        Cost = cost;
        CanUpgrade = canUpgrade;
    }
}
