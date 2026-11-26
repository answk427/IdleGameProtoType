using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

// 씬·뷰·프리팹 인스턴스 없이, 실제 게임 데이터로 게임과 같은 전투와 흐름을 돌린다(docs/COMBAT_ARCHITECTURE.md 11절 5-B, R7).
// 게임이 쓰는 것과 같은 조각만 조립한다: GameSession(진행도), CombatantSetup(데이터 → 능력치·보상), CombatWorld, EncounterDirector,
// SimulationLoop. 판정 몸(반너비, 몸 중심 오프셋)과 행동 시간표는 프리팹 에셋에서 읽기만 한다(인스턴스를 만들지 않는다).
// 스포너는 MonsterSpawner와 같은 규칙이다: 프리팹별 풀(꺼낼 때 마지막에 넣은 것부터), 스테이지를 떠나면 그 스테이지 몬스터의 풀을 비운다.
//
// 기록(Log)은 사본의 플레이 기준선과 같은 모양이다: D 피해, H 회복, K 처치(보상 골드·경험치), S 스테이지 입장. 모두 시뮬레이션 시각.
public sealed class HeadlessGame
{
    public readonly SimClock Clock;
    public readonly CombatWorld World;
    public readonly EncounterDirector Director;
    public readonly SimulationLoop Loop;
    public readonly GameSession Session;
    public readonly Combatant Player;

    public int Kills { get; private set; }
    public double LastKillTime { get; private set; } = double.NaN;
    public readonly List<double> KillTimes = new List<double>();

    private readonly StringBuilder log = new StringBuilder();
    private readonly bool recording;

    // playerPivotX: 플레이어 피벗의 처음 x(게임은 씬의 스폰 지점). 결과의 불변식에는 상관없고, 게임 기록과 비트 단위로 비교할 때만 맞춘다.
    public HeadlessGame(PlayerSaveData save, bool challengeBosses, bool record = true, float playerPivotX = 0f, int characterId = 1)
    {
        recording = record;

        var balance = Load<BalanceConfig>();
        var monsters = Load<MonsterDatabase>();
        var stages = Load<StageDatabase>();
        var skills = Load<SkillDatabase>();
        var characters = Load<CharacterDatabase>();

        Session = new GameSession(Load<PlayerStatDatabase>(), Load<PlayerUpgradeConfig>(), skills.GetAll(), Load<ContentUnlockDatabase>());
        Session.Apply(save);

        Clock = new SimClock(CombatSimulation.StepLengthOf(Time.fixedDeltaTime));
        World = new CombatWorld(Clock);

        // 플레이어: PlayerController.Initialize와 같은 계산. 월드에 먼저 등록한다(게임도 플레이어가 먼저).
        CharacterEntry character = characters.GetById(characterId);
        var playerHost = character.prefab.GetComponent<PlayerController>();
        var loadout = new SkillLoadout(PlayerStatsSaveData.SkillSlotCount, id => skills.GetById(id));
        loadout.SetSlots(Session.Stats.EquippedSlots.Value);
        Player = new Combatant(Team.Ally);
        Player.Spawn(CombatantSetup.ForPlayer(Session.Stats.CombatStats.Value, character.data),
            playerPivotX + playerHost.Hitbox.BodyOffset.x, playerHost.HalfWidth, playerHost.Timelines, loadout);
        World.Add(Player);

        Director = new EncounterDirector(World, new Spawner(monsters, skills, balance), n => stages.GetByNumber(n)?.data, FlowTimings.From(balance));
        Loop = new SimulationLoop(Clock, World, Director);

        World.Events.Subscribe(Record);
        Director.StageEntered.Subscribe(e => Line("S", Clock.Now).Append(' ').Append(e.StageNumber).Append(' ').Append(e.Reason).Append('\n'));
        // 보스 도전은 도전 가능해지는 순간(고정 단계 ① 안) 요청한다. 실행은 다음 ①이라 프레임과 무관하다.
        if (challengeBosses) Director.BossChallengeAvailable.Subscribe(available => { if (available) Director.RequestBossChallenge(); });

        Director.Begin(Session.CurrentStageNumber.Value);
    }

    // 시뮬레이션 시각 until까지 돌린다. frameSeconds(i) = i번째 화면 프레임의 길이.
    public void RunUntil(double until, Func<int, double> frameSeconds)
    {
        for (int i = 0; Clock.Now < until; i++) Loop.Advance(frameSeconds(i));
    }

    // until 미만에 일어난 일만(마지막 프레임이 넘어간 만큼은 프레임 길이에 따라 다르다).
    public string LogBefore(double until)
    {
        var cut = new StringBuilder();
        foreach (string line in log.ToString().Split('\n'))
        {
            if (line.Length == 0) continue;
            string[] parts = line.Split(' ');
            if (double.Parse(parts[1], CultureInfo.InvariantCulture) < until) cut.Append(line).Append('\n');
        }
        return cut.ToString();
    }

    public static T Load<T>() where T : UnityEngine.Object
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) return asset;
        }
        throw new InvalidOperationException($"{typeof(T).Name} 에셋이 없습니다.");
    }

    private void Record(CombatEvent e)
    {
        switch (e.Type)
        {
            case CombatEventType.Damaged:
                Line("D", e.Time).Append(' ').Append(e.Source.Team).Append(' ').Append(R(e.Amount.ToDouble())).Append('\n');
                break;
            case CombatEventType.Healed:
                Line("H", e.Time).Append(' ').Append(e.Source.Team).Append(' ').Append(R(e.Amount.ToDouble())).Append('\n');
                break;
            case CombatEventType.Died:
                if (e.Source.Team != Team.Enemy) break;
                Kills++;
                LastKillTime = e.Time;
                KillTimes.Add(e.Time);
                Line("K", e.Time).Append(' ').Append(R(e.Gold.ToDouble())).Append(' ').Append(e.Exp).Append('\n');
                break;
        }
    }

    private StringBuilder Line(string kind, double time) =>
        recording ? log.Append(kind).Append(' ').Append(R(time)) : new StringBuilder();

    private static string R(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    // MonsterSpawner와 같은 규칙(프리팹 인스턴스 대신 전투원만).
    private sealed class Spawner : IEncounterHost
    {
        private sealed class Pooled
        {
            public readonly Combatant Combatant = new Combatant(Team.Enemy);
            public SkillLoadout Skills;
            public GameObject Prefab;
        }

        private readonly MonsterDatabase monsters;
        private readonly SkillDatabase skills;
        private readonly BalanceConfig balance;
        private readonly Dictionary<GameObject, Stack<Pooled>> pools = new Dictionary<GameObject, Stack<Pooled>>();
        private readonly Dictionary<Combatant, Pooled> active = new Dictionary<Combatant, Pooled>();
        private readonly int[] slot = new int[1];

        public Spawner(MonsterDatabase monsters, SkillDatabase skills, BalanceConfig balance)
        {
            this.monsters = monsters;
            this.skills = skills;
            this.balance = balance;
        }

        public Combatant Spawn(int monsterId, StageData stage, float pivotX, bool isBoss)
        {
            MonsterEntry entry = monsters.GetById(monsterId);
            if (entry?.data == null || entry.prefab == null) return null;

            if (!pools.TryGetValue(entry.prefab, out Stack<Pooled> pool)) pools[entry.prefab] = pool = new Stack<Pooled>();
            Pooled p = pool.Count > 0 ? pool.Pop() : new Pooled { Prefab = entry.prefab, Skills = new SkillLoadout(1, id => skills.GetById(id)) };

            MonsterData data = entry.data;
            slot[0] = data.SkillId > 0 ? data.SkillId : SkillLoadout.EmptySlot;
            p.Skills.SetSlots(slot);
            p.Skills.ResetCooldowns();

            var host = entry.prefab.GetComponent<Monster>();
            CombatantSetup.StageMultipliers(stage.StageNumber, balance, out double hp, out double damage, out double gold);
            p.Combatant.Spawn(CombatantSetup.ForMonster(data, hp, damage), pivotX + host.Hitbox.BodyOffset.x, host.HalfWidth, host.Timelines,
                p.Skills, CombatantSetup.RewardOf(data, gold));

            active[p.Combatant] = p;
            return p.Combatant;
        }

        public void Despawn(Combatant combatant)
        {
            if (!active.TryGetValue(combatant, out Pooled p)) return;
            active.Remove(combatant);
            pools[p.Prefab].Push(p);
        }

        public void ReleaseStage(StageData stage)
        {
            foreach (int monsterId in new[] { stage.NormalMonsterId, stage.BossMonsterId })
            {
                MonsterEntry entry = monsters.GetById(monsterId);
                if (entry?.prefab != null && pools.TryGetValue(entry.prefab, out Stack<Pooled> pool)) pool.Clear();
            }
        }
    }
}
