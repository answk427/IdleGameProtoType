using System;
using System.Collections.Generic;

// 게임(진행도, 전투, 흐름)이 읽는 데이터 한 벌(docs/COMBAT_ARCHITECTURE.md 12절 D46, 13-4).
// 시작할 때 GameTables에서 한 번 만들어 Game에 넘긴다. 정적 조회는 없다(새 코드가 아무 곳에서나 데이터를 찾지 못한다).
//
// 두 가지를 낸다.
//   - 데이터 행: 진행도·UI가 읽는다(플레이어 능력치 표, 업그레이드, 해금, 스킬 행, 몬스터·캐릭터 행).
//   - 시뮬레이션 입력(사양): 데이터 행 → 시뮬레이션이 쓰는 값으로 바꾸는 곳은 여기 한 곳이다(D37). 시뮬레이션은 데이터 행과 에셋을 모른다.
//     스킬·스테이지 사양과 행동 시간표는 시작 때 만들어 두고(스폰·장착에 할당 없음, R6), 튜닝 덮어쓰기가 데이터를 바꾸면 RefreshSpecs로 다시 만든다.
//     전투원 사양(능력치 × 스테이지 배율, 보상, 몸)은 값 구조체라 스폰할 때마다 계산한다(튜닝이 다음 스폰에 바로 들어간다).
// 그림 자산(프리팹, 아이콘, VFX, 배경)은 뷰 쪽 Presentation이 같은 표에서 꺼낸다(D47).
public sealed class GameData
{
    private readonly GameTables tables;
    private readonly List<SkillData> skillRows = new List<SkillData>();
    private readonly Dictionary<int, SkillSpec> skills = new Dictionary<int, SkillSpec>();
    private readonly Dictionary<int, StageSpec> stages = new Dictionary<int, StageSpec>();
    private readonly Dictionary<ActionTimelineSet, ActionTimingSet> timings = new Dictionary<ActionTimelineSet, ActionTimingSet>();

    public GameData(GameTables tables)
    {
        this.tables = tables ?? throw new ArgumentNullException(nameof(tables));
        foreach (SkillEntry entry in tables.Skills.GetAll())
        {
            if (entry?.data != null) skillRows.Add(entry.data);
        }

        RefreshSpecs();
        foreach (MonsterEntry entry in tables.Monsters.entries) TimingsOf(entry?.timelines);
        foreach (CharacterEntry entry in tables.Characters.entries) TimingsOf(entry?.timelines);
    }

    // ── 데이터 행 (진행도·UI) ──

    public PlayerStatDatabase PlayerStats => tables.PlayerStats;
    public PlayerUpgradeConfig Upgrades => tables.Upgrades;
    public ContentUnlockDatabase Unlocks => tables.Unlocks;
    public BalanceConfig Balance => tables.Balance;

    // 스킬 표의 데이터 행 전부(학습 조건, 배울 스킬 있음)
    public IReadOnlyList<SkillData> SkillRows => skillRows;

    public MonsterData Monster(int id) => tables.Monsters.GetById(id)?.data;
    public CharacterData Character(int id) => tables.Characters.GetById(id)?.data;

    // ── 시뮬레이션 입력 ──

    // 스테이지 사양. 없으면 null.
    public StageSpec Stage(int stageNumber) => stages.TryGetValue(stageNumber, out StageSpec spec) ? spec : null;

    // 스킬 사양. 없으면 null.
    public SkillSpec Skill(int id) => skills.TryGetValue(id, out SkillSpec spec) ? spec : null;

    // 흐름의 대기 시간(D24)
    public FlowTimings FlowTimings =>
        new FlowTimings(tables.Balance.encounterClearDelay, tables.Balance.bossIntroDelay, tables.Balance.transitionDuration);

    // 몬스터 하나의 사양(일반, 보스 공통). 차이는 데이터다: 공격 간격 > 0이면 기본 공격, movePolicy가 이동 방식.
    // 능력치와 골드는 스테이지 배율로 지수적으로 커진다(double로 계산해 GameNumber로, float 지수는 큰 스테이지에서 정밀도를 잃는다).
    public bool TryGetMonster(int monsterId, int stageNumber, bool isBoss, out CombatantSpec spec, out string error)
    {
        spec = default;
        MonsterEntry entry = tables.Monsters.GetById(monsterId);
        MonsterData monster = entry?.data;
        if (monster == null)
        {
            error = $"몬스터 {monsterId}가 MonsterDatabase에 없습니다";
            return false;
        }
        ActionTimingSet monsterTimings = TimingsOf(entry.timelines);
        if (monsterTimings == null)
        {
            error = $"몬스터 {monsterId}({monster.MonsterName})의 행동 시간표가 연결되지 않았습니다(MonsterDatabase 항목의 timelines)";
            return false;
        }

        BalanceConfig balance = tables.Balance;
        double hp = Math.Pow(balance.hpGrowthBase, stageNumber);
        double damage = Math.Pow(balance.damageGrowthBase, stageNumber);
        double gold = Math.Pow(balance.goldGrowthBase, stageNumber);

        var stats = new CombatantStats
        {
            MaxHp = GameNumber.Round(monster.MaxHp * hp),
            AttackDamage = GameNumber.Round(monster.AttackDamage * damage),
            AttackInterval = monster.AttackInterval,
            AttackRange = monster.AttackRange,
            MoveSpeed = monster.MoveSpeed, // 접근 정책이 쓴다(제자리 정책은 움직이지 않는다)
            MovePolicy = monster.MovePolicy,
        };
        var reward = new CombatReward(GameNumber.Round(monster.GoldReward * gold), monster.ExpReward);
        spec = new CombatantSpec(CombatantArchetype.Monster(monsterId, isBoss), stats, new CombatantBody(monster.HalfWidth, monsterTimings), reward);
        error = null;
        return true;
    }

    // 플레이어 캐릭터의 사양. 캐릭터 표에 없거나 시간표가 연결되지 않았으면 false.
    public bool TryGetPlayer(int characterId, in PlayerCombatStats progress, out CombatantSpec spec)
    {
        spec = default;
        CharacterEntry entry = tables.Characters.GetById(characterId);
        ActionTimingSet characterTimings = TimingsOf(entry?.timelines);
        if (entry?.data == null || characterTimings == null) return false;

        spec = new CombatantSpec(CombatantArchetype.Character(characterId), PlayerCombatantStats(progress, entry.data),
            new CombatantBody(entry.data.HalfWidth, characterTimings));
        return true;
    }

    // 플레이어의 능력치: 레벨·업그레이드로 변하는 것(HP, 공격력, 공격 간격)은 진행도에서, 캐릭터마다 다른 것(사거리, 이동 속도)은 캐릭터 표에서.
    // 진행도가 바뀔 때(레벨업, 업그레이드, 교체, 튜닝) 다시 부른다. 캐릭터가 없으면 사거리·이동 속도 0.
    public CombatantStats PlayerStatsOf(int characterId, in PlayerCombatStats progress) => PlayerCombatantStats(progress, Character(characterId));

    // 튜닝 덮어쓰기가 데이터 행을 바꿨을 때 사양을 다시 만든다(13-4). 사양을 쓰는 쪽은 입력이 들어가는 시점에 새 값을 받는다:
    // 플레이어 스킬은 장착을 다시 맞출 때(Game.RecomputeFromData), 몬스터는 다음 스폰, 스테이지는 다음 입장.
    public void RefreshSpecs()
    {
        skills.Clear();
        foreach (SkillData row in skillRows)
        {
            skills[row.Id] = new SkillSpec(row.Id, row.SkillName, row.Cooldown, row.CastTime, row.AnimationTrigger, row.EffectType, row.Value1, row.Value2);
        }

        stages.Clear();
        foreach (StageEntry entry in tables.Stages.entries)
        {
            StageData row = entry?.data;
            if (row == null) continue;
            stages[row.StageNumber] = new StageSpec(row.StageNumber, row.NormalMonsterId, row.BossMonsterId, row.MonstersPerEncounter,
                row.EncountersToComplete, row.MonsterSpacing, row.SpawnDistance, row.BossTimeLimit, row.ClearType);
        }
    }

    private static CombatantStats PlayerCombatantStats(in PlayerCombatStats progress, CharacterData character) => new CombatantStats
    {
        MaxHp = progress.MaxHp,
        AttackDamage = progress.AttackDamage,
        AttackInterval = progress.AttackInterval,
        AttackRange = character != null ? character.AttackRange : 0f,
        MoveSpeed = character != null ? character.MoveSpeed : 0f,
        MovePolicy = MovePolicy.Advance,
    };

    // 행동 시간표 에셋 → 시뮬레이션 입력. 에셋마다 한 번만 바꾼다(시간표는 튜닝 대상이 아니다).
    private ActionTimingSet TimingsOf(ActionTimelineSet set)
    {
        if (set == null) return null;
        if (timings.TryGetValue(set, out ActionTimingSet cached)) return cached;

        var list = new List<ActionTiming>(set.Timelines.Count);
        foreach (ActionTimeline timeline in set.Timelines)
        {
            if (timeline != null) list.Add(new ActionTiming(timeline.Key, timeline.ClipLength, timeline.HitFractions));
        }
        var converted = new ActionTimingSet(list);
        timings.Add(set, converted);
        return converted;
    }
}
