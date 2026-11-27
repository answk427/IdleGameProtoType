using System.Collections.Generic;

// 그림 자산의 조회(docs/COMBAT_ARCHITECTURE.md 12절 D46, D47): 전투원 프리팹과 바닥 보정, 스킬의 아이콘·VFX·소리, 스테이지 배경.
// 뷰(BattleView), 이펙트(CombatEffectManager), 배경, UI가 쓴다. 게임 규칙(Game, 시뮬레이션, 진행도)은 이것을 모른다.
// GameData와 같은 표(GameTables)에서 만든다.
public sealed class Presentation
{
    private readonly GameTables tables;

    public Presentation(GameTables tables)
    {
        this.tables = tables ?? throw new System.ArgumentNullException(nameof(tables));
    }

    public MonsterEntry Monster(int id) => tables.Monsters.GetById(id);
    public CharacterEntry Character(int id) => tables.Characters.GetById(id);
    public SkillEntry Skill(int id) => tables.Skills.GetById(id);
    public IReadOnlyList<SkillEntry> Skills => tables.Skills.GetAll();
    public StageEntry Stage(int stageNumber) => tables.Stages.GetByNumber(stageNumber);
}
