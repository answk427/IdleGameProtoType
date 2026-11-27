// 준비된 데이터 표 한 벌(docs/COMBAT_ARCHITECTURE.md 12절 D46). GameDataAssets.Prepare가 만든다.
// 게임 쪽은 이것으로 GameData(데이터 행, 전투원의 몸)를, 뷰 쪽은 Presentation(프리팹, 아이콘, VFX, 배경)을 만든다.
// 둘이 같은 표(에디터 플레이에서는 같은 튜닝 사본)를 본다.
public sealed class GameTables
{
    public readonly PlayerStatDatabase PlayerStats;
    public readonly PlayerUpgradeConfig Upgrades;
    public readonly MonsterDatabase Monsters;
    public readonly StageDatabase Stages;
    public readonly SkillDatabase Skills;
    public readonly CharacterDatabase Characters;
    public readonly ContentUnlockDatabase Unlocks;
    public readonly BalanceConfig Balance;

    public GameTables(PlayerStatDatabase playerStats, PlayerUpgradeConfig upgrades, MonsterDatabase monsters, StageDatabase stages,
        SkillDatabase skills, CharacterDatabase characters, ContentUnlockDatabase unlocks, BalanceConfig balance)
    {
        PlayerStats = playerStats;
        Upgrades = upgrades;
        Monsters = monsters;
        Stages = stages;
        Skills = skills;
        Characters = characters;
        Unlocks = unlocks;
        Balance = balance;

        monsters.EnsureLookup();
        stages.EnsureLookup();
        skills.EnsureLookup();
        characters.EnsureLookup();
        unlocks.EnsureLookup();
    }
}
