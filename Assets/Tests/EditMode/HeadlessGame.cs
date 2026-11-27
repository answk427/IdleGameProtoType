using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

// 씬·뷰·프리팹 인스턴스 없이 실제 게임 데이터로 게임(Game)을 돌리고 기록한다(docs/COMBAT_ARCHITECTURE.md 11절 5-B, 12절, R7).
// 게임 쪽 조립(진행도, 전투, 흐름, 적 풀, 처치 보상, 저장 계기)은 Game 그대로다. 여기에 복제한 규칙은 없다:
// 데이터도 게임과 같은 에셋(GameDataAssets)에서 같은 방법(Prepare)으로 만든다.
//
// 기록(Log)은 사본의 플레이 기준선과 같은 모양이다: D 피해, H 회복, K 처치(보상 골드·경험치), S 스테이지 입장. 모두 시뮬레이션 시각.
public sealed class HeadlessGame
{
    public readonly Game Game;

    public int Kills { get; private set; }
    public double LastKillTime { get; private set; } = double.NaN;
    public readonly List<double> KillTimes = new List<double>();

    private readonly StringBuilder log = new StringBuilder();
    private readonly bool recording;

    public HeadlessGame(PlayerSaveData save, bool challengeBosses, bool record = true, int characterId = 1)
    {
        recording = record;

        var errors = new List<string>();
        GameTables tables = Load<GameDataAssets>().Prepare(errors);
        if (tables == null) throw new InvalidOperationException(string.Join("\n", errors));

        var options = new GameOptions(characterId, SimClock.StepLengthFromSetting(Time.fixedDeltaTime));
        GameStart start = Game.TryCreate(new GameData(tables), save, options);
        if (!start.Succeeded) throw new InvalidOperationException($"게임을 시작하지 못했습니다: {start.Error}(스테이지 {start.SavedStageNumber})");
        Game = start.Game;

        Game.World.Events.Subscribe(Record);
        Game.Flow.StageEntered.Subscribe(e => Line("S", Clock.Now).Append(' ').Append(e.StageNumber).Append(' ').Append(e.Reason).Append('\n'));
        // 보스 도전은 도전 가능해지는 순간(고정 단계 ① 안) 요청한다. 실행은 다음 ①이라 프레임과 무관하다.
        if (challengeBosses) Game.Flow.BossChallengeAvailable.Subscribe(available => { if (available) Game.Flow.RequestBossChallenge(); });

        Game.Begin();
    }

    public SimClock Clock => Game.Clock;

    // 시뮬레이션 시각 until까지 돌린다. frameSeconds(i) = i번째 화면 프레임의 길이.
    public void RunUntil(double until, Func<int, double> frameSeconds)
    {
        for (int i = 0; Clock.Now < until; i++) Game.Advance(frameSeconds(i));
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
}
