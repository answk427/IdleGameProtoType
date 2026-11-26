using System;

// 데이터 → 전투원 입력(능력치, 처치 보상)의 계산 한 곳(docs/COMBAT_ARCHITECTURE.md 11절 5-B).
// 게임(MonsterSpawner, Monster, PlayerController)과 씬 없는 실행(헤드리스 시험, 이후 오프라인 보상)이 같은 계산을 쓴다.
// 예전에는 몬스터 호스트, 스포너, 플레이어 호스트에 나뉘어 있어서 씬 없이 같은 전투를 만들 수 없었다.
public static class CombatantSetup
{
    // 스테이지 성장 배율. double로 계산한다(float 지수는 큰 스테이지에서 정밀도를 잃고, 결과는 GameNumber로 간다).
    public static void StageMultipliers(int stageNumber, BalanceConfig balance, out double hp, out double damage, out double gold)
    {
        if (balance == null) throw new ArgumentNullException(nameof(balance));
        hp = Math.Pow(balance.hpGrowthBase, stageNumber);
        damage = Math.Pow(balance.damageGrowthBase, stageNumber);
        gold = Math.Pow(balance.goldGrowthBase, stageNumber);
    }

    // 몬스터(일반, 보스 공통). 차이는 데이터다: 공격 간격 > 0이면 기본 공격, movePolicy가 이동 방식.
    public static CombatantStats ForMonster(MonsterData data, double hpMultiplier, double damageMultiplier) => new CombatantStats
    {
        MaxHp = GameNumber.Round(data.MaxHp * hpMultiplier),
        AttackDamage = GameNumber.Round(data.AttackDamage * damageMultiplier),
        AttackInterval = data.AttackInterval,
        AttackRange = data.AttackRange,
        MoveSpeed = data.MoveSpeed, // 접근 정책이 쓴다(제자리 정책은 움직이지 않는다)
        MovePolicy = data.MovePolicy,
    };

    // 처치 보상. 골드는 스테이지 배율로 지수적으로 커지므로 GameNumber.
    public static CombatReward RewardOf(MonsterData data, double goldMultiplier) =>
        new CombatReward(GameNumber.Round(data.GoldReward * goldMultiplier), data.ExpReward);

    // 플레이어: 레벨·업그레이드로 변하는 것(HP, 공격력, 공격 간격)은 진행도에서, 캐릭터마다 다른 것(사거리, 이동 속도)은 캐릭터 표에서.
    public static CombatantStats ForPlayer(in PlayerCombatStats stats, CharacterData character) => new CombatantStats
    {
        MaxHp = stats.MaxHp,
        AttackDamage = stats.AttackDamage,
        AttackInterval = stats.AttackInterval,
        AttackRange = character != null ? character.AttackRange : 0f,
        MoveSpeed = character != null ? character.MoveSpeed : 0f,
        MovePolicy = MovePolicy.Advance,
    };
}
