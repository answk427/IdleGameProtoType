using System.Collections.Generic;

// 게임(진행도, 전투, 흐름)이 읽는 데이터 한 벌(docs/COMBAT_ARCHITECTURE.md 12절 D46).
// 시작할 때 GameTables에서 한 번 만들어 Game에 넘긴다. 정적 조회는 없다(새 코드가 아무 곳에서나 데이터를 찾지 못한다).
// 데이터 행(…Data)과 전투원의 몸만 준다. 그림 자산(프리팹, 아이콘, VFX, 배경)은 뷰 쪽 Presentation이 같은 표에서 꺼낸다(D47).
public sealed class GameData
{
    private readonly GameTables tables;
    private readonly List<SkillData> skills = new List<SkillData>();

    public GameData(GameTables tables)
    {
        this.tables = tables ?? throw new System.ArgumentNullException(nameof(tables));
        foreach (SkillEntry entry in tables.Skills.GetAll())
        {
            if (entry?.data != null) skills.Add(entry.data);
        }
    }

    public PlayerStatDatabase PlayerStats => tables.PlayerStats;
    public PlayerUpgradeConfig Upgrades => tables.Upgrades;
    public ContentUnlockDatabase Unlocks => tables.Unlocks;
    public BalanceConfig Balance => tables.Balance;

    // 스킬 표의 데이터 행 전부(학습 조건, 배울 스킬 있음).
    public IReadOnlyList<SkillData> Skills => skills;

    public MonsterData Monster(int id) => tables.Monsters.GetById(id)?.data;
    public StageData Stage(int stageNumber) => tables.Stages.GetByNumber(stageNumber)?.data;
    public SkillData Skill(int id) => tables.Skills.GetById(id)?.data;
    public CharacterData Character(int id) => tables.Characters.GetById(id)?.data;

    // 전투원의 몸(반너비, 행동 시간표). 반너비는 데이터 행(엑셀), 시간표는 데이터 항목의 연결이다(D50).
    // 프리팹은 읽지 않는다: 게임과 씬 없는 실행이 그림 자산 없이 같은 전투를 만든다. 시간표가 없으면 false(데이터 검사가 먼저 막는다).
    public bool TryGetMonsterBody(int id, out CombatantBody body)
    {
        MonsterEntry entry = tables.Monsters.GetById(id);
        body = entry?.data != null ? new CombatantBody(entry.data.HalfWidth, entry.timelines) : default;
        return entry?.data != null && entry.timelines != null;
    }

    public bool TryGetCharacterBody(int id, out CombatantBody body)
    {
        CharacterEntry entry = tables.Characters.GetById(id);
        body = entry?.data != null ? new CombatantBody(entry.data.HalfWidth, entry.timelines) : default;
        return entry?.data != null && entry.timelines != null;
    }
}
