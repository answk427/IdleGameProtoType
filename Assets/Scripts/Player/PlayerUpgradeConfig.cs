using UnityEngine;

/// <summary>
/// 업그레이드 설정 (ScriptableObject)
/// 각 스탯 업그레이드의 효과량과 비용 공식 정의
/// </summary>
[CreateAssetMenu(fileName = "PlayerUpgradeConfig", menuName = "IdleGame/Player Upgrade Config")]
public class PlayerUpgradeConfig : ScriptableObject
{
    [System.Serializable]
    public class UpgradeSetting
    {
        [Header("업그레이드당 증가량")]
        public int hpPerUpgrade = 20;
        public int attackPerUpgrade = 3;
        public float speedPerUpgrade = 0.1f;

        [Header("비용 공식: cost = baseCost * (costMultiplier ^ level)")]
        public int hpBaseCost = 50;
        public float hpCostMultiplier = 1.15f;

        public int attackBaseCost = 80;
        public float attackCostMultiplier = 1.18f;

        public int speedBaseCost = 100;
        public float speedCostMultiplier = 1.20f;
    }

    [Header("최대 업그레이드 횟수 (0 = 무제한)")]
    public int maxUpgradeLevel = 0;

    [SerializeField] public UpgradeSetting settings = new();

    public int GetHpUpgradeCost(int currentLevel)
        => Mathf.RoundToInt(settings.hpBaseCost * Mathf.Pow(settings.hpCostMultiplier, currentLevel));

    public int GetAttackUpgradeCost(int currentLevel)
        => Mathf.RoundToInt(settings.attackBaseCost * Mathf.Pow(settings.attackCostMultiplier, currentLevel));

    public int GetSpeedUpgradeCost(int currentLevel)
        => Mathf.RoundToInt(settings.speedBaseCost * Mathf.Pow(settings.speedCostMultiplier, currentLevel));
}
