using System;
using UnityEngine;

// 업그레이드 설정
// 각 스탯 업그레이드의 효과량과 비용 공식 정의
[CreateAssetMenu(fileName = "PlayerUpgradeConfig", menuName = "IdleGame/Player Upgrade Config")]
public class PlayerUpgradeConfig : ScriptableObject
{
    [System.Serializable]
    public class UpgradeSetting
    {
        [Header("업그레이드당 증가량")]
        public int hpPerUpgrade = 20;
        public int attackPerUpgrade = 3;
        public float attackSpeedPerUpgrade = 0.02f; // 공격 간격(초) 감소량

        [Header("비용 공식: cost = baseCost * (costMultiplier ^ level)")]
        public int hpBaseCost = 50;
        public float hpCostMultiplier = 1.15f;

        public int attackBaseCost = 80;
        public float attackCostMultiplier = 1.18f;

        public int attackSpeedBaseCost = 80;
        public float attackSpeedCostMultiplier = 1.18f;
    }

    [Header("최대 업그레이드 횟수 (0 = 무제한)")]
    public int maxUpgradeLevel = 0;

    [SerializeField] public UpgradeSetting settings = new();

    // 비용은 지수적으로 커지므로 int가 아니라 GameNumber로 계산한다(int는 약 21억에서 넘친다).
    public GameNumber GetHpUpgradeCost(int currentLevel)
        => CalculateCost(settings.hpBaseCost, settings.hpCostMultiplier, currentLevel);

    public GameNumber GetAttackUpgradeCost(int currentLevel)
        => CalculateCost(settings.attackBaseCost, settings.attackCostMultiplier, currentLevel);

    public GameNumber GetAttackSpeedUpgradeCost(int currentLevel)
        => CalculateCost(settings.attackSpeedBaseCost, settings.attackSpeedCostMultiplier, currentLevel);

    private static GameNumber CalculateCost(int baseCost, float multiplier, int level)
        => GameNumber.Round(baseCost * Math.Pow(multiplier, level));
}
