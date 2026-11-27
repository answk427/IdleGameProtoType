using System;
using System.Collections.Generic;
using UnityEngine;

// 개발 도구의 명령(docs/DEV_TOOLS.md T7). 화면(DevPanel)은 이것을 부르기만 한다.
// 에디터에서는 DevTools.Commands로 같은 명령을 코드에서 부를 수 있다(Pipeline script eval 등).
//
// 원칙(2장): 개발 도구 전용 게임 API를 만들지 않는다.
//   - 더하는 명령은 게임의 보상 경로(Wallet.AddGold, PlayerStats.AddExp)를 그대로 쓴다.
//   - 값을 통째로 바꾸는 명령(새로 시작, 복원, 레벨 지정)은 모두 Game.LoadSnapshot 하나를 지난다.
//   - 덮어쓰기 전에는 지금 진행도를 백업한다. 백업이 실패하면 덮어쓰지 않는다.
public sealed class DevCommands
{
    public static readonly float[] TimeScales = { 1f, 2f, 5f, 10f };

    private readonly Game game;
    private readonly GameSession session;
    private readonly EncounterDirector flow;
    private readonly DevBackupStore backups;

    public DevCommands(Game game, DevBackupStore backups)
    {
        this.game = game ?? throw new ArgumentNullException(nameof(game));
        session = game.Session;
        flow = game.Flow;
        this.backups = backups ?? throw new ArgumentNullException(nameof(backups));
    }

    // 마지막 명령의 결과. 패널이 보여 주고, 콘솔에도 남긴다.
    public string LastMessage { get; private set; } = "";

    public GameSession Session => session;
    public DevBackupStore Backups => backups;
    public bool IsBusy => flow.Phase.Value == FlowPhase.Transition;
    public FlowPhase Phase => flow.Phase.Value;

    // ── 스테이지 (흐름의 명령을 그대로 부른다, 실행은 다음 고정 단계) ──

    // 지금 플레이 중인 스테이지(흐름 기준). 진행도 교체 직후 전환 가운데까지는 세션의 스테이지 번호와 다를 수 있다.
    public int CurrentStage => flow.CurrentStageNumber.Value;

    // 스테이지 표에 있는 스테이지인가(이동 입력 검사).
    public bool HasStage(int stageNumber) => stageNumber >= 1 && game.Data.Stage(stageNumber) != null;

    public bool GoToStage(int stageNumber)
    {
        RestartResult result = flow.RestartAt(stageNumber);
        return Report(result == RestartResult.Accepted, $"스테이지 {stageNumber} 시작",
            result == RestartResult.NoSuchStage ? $"스테이지 {stageNumber}가 없습니다." : "스테이지 전환 중입니다. 끝난 뒤 다시 하세요.");
    }

    public bool RestartStage() => GoToStage(CurrentStage);

    // 보스 도전 버튼과 같은 명령이다(D27): 인카운터를 다 깨서 도전 가능할 때만 받는다.
    // 개발 도구 전용 입구("보스부터 시작")를 만들지 않는다. 빨리 보려면 배속을 올린다.
    public bool ChallengeBoss()
    {
        switch (flow.RequestBossChallenge())
        {
            case BossChallengeResult.Accepted:
                return Report(true, $"스테이지 {CurrentStage} 보스 도전", "");
            case BossChallengeResult.NotAvailable:
                if (flow.Phase.Value == FlowPhase.BossIntro || flow.Phase.Value == FlowPhase.Boss)
                    return Report(false, "", "이미 보스전입니다.");
                int needed = flow.Stage != null ? flow.Stage.EncountersToComplete : 0;
                if (flow.EncountersCleared >= needed)
                    return Report(false, "", "보스 도전을 이미 받았습니다(다음 단계에 시작).");
                return Report(false, "", $"아직 보스 도전 가능이 아닙니다(인카운터 {flow.EncountersCleared}/{needed}). 배속을 올리면 빨리 됩니다.");
            default:
                return Report(false, "", $"지금은 보스 도전을 받을 수 없습니다({flow.Phase.Value}).");
        }
    }

    // ── 진행도(더하기: 보상 경로) ─────────────────────────

    // 지금 골드의 10배가 되게 더한다(최소 1만). 방치형 경제는 지수적으로 커져서 고정량보다 쓸모 있다.
    public void MultiplyGold()
    {
        double gold = session.Wallet.Gold.Value.ToDouble();
        double amount = Math.Max(gold * 9d, 10000d);
        session.Wallet.AddGold(GameNumber.FromDouble(amount));
        Report(true, $"골드 +{NumberFormatUtility.Format(GameNumber.FromDouble(amount))}", "");
    }

    // 지금 레벨의 필요 경험치만큼 더한다. 레벨업은 게임의 LEVEL UP 버튼으로 한다.
    public bool AddExpForOneLevel()
    {
        PlayerStats stats = session.Stats;
        if (stats.IsMaxLevel.Value) return Report(false, "", "최대 레벨입니다.");

        int amount = stats.RequiredExp.Value;
        stats.AddExp(amount);
        return Report(true, $"경험치 +{amount} (LEVEL UP 버튼으로 레벨업)", "");
    }

    // ── 진행도(바꾸기: LoadSnapshot) ──────────────────────

    public int MaxLevel => session.Stats.MaxLevel;

    // 레벨만 바꾸고 경험치는 0. 나머지(업그레이드, 스킬, 골드, 스테이지)는 그대로. 지금 스테이지를 처음부터 다시 한다.
    public bool SetLevel(int level)
    {
        level = Mathf.Clamp(level, 1, MaxLevel);
        if (!TryBackupBeforeReplace("before-level")) return false;

        PlayerSaveData snapshot = CopyOfCurrent();
        snapshot.stats.level = level;
        snapshot.stats.currentExp = 0;
        return Replace(snapshot, $"레벨 {level}로 바꿈");
    }

    // ── 세이브 ────────────────────────────────────────────

    public bool Backup()
    {
        bool ok = backups.TryWrite(session.ToSaveData(), "manual", out string path, out string error);
        return Report(ok, $"백업함: {System.IO.Path.GetFileName(path)}", error);
    }

    public IReadOnlyList<string> ListBackups() => backups.List();

    public bool Restore(string backupPath)
    {
        if (!backups.TryRead(backupPath, out PlayerSaveData data, out string error)) return Report(false, "", error);
        if (!TryBackupBeforeReplace("before-restore")) return false;
        return Replace(data, $"복원함: {System.IO.Path.GetFileName(backupPath)}");
    }

    // 처음 설치한 것과 같은 진행도(세이브 파일이 없을 때 LocalFileSaveStorage가 주는 것)로 바꾼다.
    public bool StartOver()
    {
        if (!TryBackupBeforeReplace("before-reset")) return false;
        return Replace(new PlayerSaveData(), "새로 시작함(레벨 1, 스테이지 1)");
    }

    // ── 시간 ──────────────────────────────────────────────

    // 전투는 고정 단계라 배속을 바꿔도 결과(처치·타격 시각)는 같고, 화면 프레임당 단계 수만 늘어난다.
    // 프레임당 단계는 Maximum Allowed Timestep(1/3초)에서 막혀, 느린 기기의 높은 배속은 요청보다 느리게 돈다.
    public float TimeScale
    {
        get => Time.timeScale;
        set
        {
            Time.timeScale = Mathf.Max(0f, value);
            Report(true, $"배속 x{Time.timeScale:0.#}", "");
        }
    }

    // ── 내부 ──────────────────────────────────────────────

    private bool TryBackupBeforeReplace(string reason)
    {
        if (IsBusy) return Report(false, "", "스테이지 전환 중입니다. 끝난 뒤 다시 하세요.");

        if (backups.TryWrite(session.ToSaveData(), reason, out _, out string error)) return true;
        return Report(false, "", $"백업하지 못해 진행도를 바꾸지 않았습니다: {error}");
    }

    private bool Replace(PlayerSaveData snapshot, string success)
    {
        bool ok = game.LoadSnapshot(snapshot, out string error);
        return Report(ok, success, error);
    }

    // ToSaveData()는 지금 세션의 데이터를 그대로 돌려준다(복사본이 아님). 고치기 전에 저장 형식으로 한 번 복사한다.
    private PlayerSaveData CopyOfCurrent() =>
        JsonUtility.FromJson<PlayerSaveData>(JsonUtility.ToJson(session.ToSaveData()));

    private bool Report(bool ok, string success, string failure)
    {
        LastMessage = ok ? success : failure;
        if (ok) Debug.Log($"[DevTools] {LastMessage}");
        else Debug.LogWarning($"[DevTools] {LastMessage}");
        return ok;
    }
}
