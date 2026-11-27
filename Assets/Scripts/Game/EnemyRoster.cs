using System.Collections.Generic;
using UnityEngine;

// 적 전투원의 주인(docs/COMBAT_ARCHITECTURE.md 12절 D44). 흐름의 스폰 접점(IEnemyRoster)이다.
// 무엇을, 몇 마리, 어디에, 언제 스폰할지는 EncounterDirector가 정하고, 여기는 몬스터 id별 풀에서 전투원을 꺼내
// 데이터(몬스터 표 × 스테이지 배율 → 능력치와 처치 보상, CombatantSetup)를 넣어 돌려준다. 월드 등록·해제는 디렉터가 한다.
// 순수 C#이고 화면을 모른다. 뷰(BattleView)는 월드의 Joined·Left 사건으로 이 전투원에 그림을 붙이고 뗀다.
// 예전에는 이 일을 MonoBehaviour 스포너가 GameObject 풀로 했고, 전투원은 몬스터 GameObject가 만들었다(12-1 F1).
public sealed class EnemyRoster : IEnemyRoster
{
    // 풀의 한 칸: 전투원과 그 스킬 슬롯(몬스터는 데이터의 스킬 1칸). 같은 객체를 다시 쓴다(전투 중 할당 0, R6).
    private sealed class Pooled
    {
        public readonly Combatant Combatant = new Combatant(Team.Enemy);
        public SkillLoadout Skills;
        public int MonsterId;
    }

    private readonly GameData data;
    private readonly Dictionary<int, Stack<Pooled>> pools = new Dictionary<int, Stack<Pooled>>();
    private readonly Dictionary<Combatant, Pooled> active = new Dictionary<Combatant, Pooled>();
    private readonly int[] skillSlot = new int[1];

    public EnemyRoster(GameData data)
    {
        this.data = data ?? throw new System.ArgumentNullException(nameof(data));
    }

    public int ActiveCount => active.Count;

    // x = 몸 중심(디렉터가 정한다). 그림의 피벗은 뷰가 맞춘다.
    public Combatant Spawn(int monsterId, StageData stage, float x, bool isBoss)
    {
        MonsterData monster = data.Monster(monsterId);
        if (monster == null)
        {
            Debug.LogError($"[EnemyRoster] 몬스터 {monsterId}가 MonsterDatabase에 없습니다(스테이지 {stage.StageNumber}).");
            return null;
        }
        if (!data.TryGetMonsterBody(monsterId, out CombatantBody body))
        {
            Debug.LogError($"[EnemyRoster] 몬스터 {monsterId}({monster.MonsterName})의 행동 시간표가 연결되지 않았습니다(MonsterDatabase 항목의 timelines).");
            return null;
        }

        if (!pools.TryGetValue(monsterId, out Stack<Pooled> pool)) pools[monsterId] = pool = new Stack<Pooled>();
        Pooled p = pool.Count > 0 ? pool.Pop() : new Pooled { MonsterId = monsterId, Skills = new SkillLoadout(1, data.Skill) };

        // 같은 객체를 다시 쓰므로 이전 생의 쿨다운을 지운다.
        skillSlot[0] = monster.SkillId > 0 ? monster.SkillId : SkillLoadout.EmptySlot;
        p.Skills.SetSlots(skillSlot);
        p.Skills.ResetCooldowns();

        CombatantSetup.StageMultipliers(stage.StageNumber, data.Balance, out double hp, out double damage, out double gold);
        p.Combatant.Spawn(CombatantSetup.ForMonster(monster, hp, damage), x, body.HalfWidth, body.Timelines,
            p.Skills, CombatantSetup.RewardOf(monster, gold), CombatantArchetype.Monster(monsterId, isBoss));

        active[p.Combatant] = p;
        return p.Combatant;
    }

    // 디렉터가 월드에서 먼저 뺀 뒤에 부른다. 풀로 돌려놓는다(뷰는 Left 사건으로 떨어진다).
    public void Despawn(Combatant combatant)
    {
        if (combatant == null || !active.TryGetValue(combatant, out Pooled p)) return;
        active.Remove(combatant);
        pools[p.MonsterId].Push(p);
    }

    // 스테이지를 떠날 때 그 스테이지 몬스터의 풀을 비운다.
    public void ReleaseStage(StageData stage)
    {
        if (pools.TryGetValue(stage.NormalMonsterId, out Stack<Pooled> normal)) normal.Clear();
        if (pools.TryGetValue(stage.BossMonsterId, out Stack<Pooled> boss)) boss.Clear();
    }
}
