using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 개발자 도구 패널(IMGUI, docs/DEV_TOOLS.md T3). 명령은 DevCommands가 하고 여기는 그리기만 한다.
//
// 게임 UI 규칙("새 팝업은 UIWindow")의 예외다. 릴리스 빌드에서 통째로 빠져야 해서 에셋(프리팹, WindowRegistry 등록)을
// 가질 수 없다. 크기는 화면 짧은 변 360을 기준(1dp)으로 맞춰 손가락으로 누를 수 있게 한다.
// 덮어쓰는 명령(레벨 지정, 새로 시작, 복원)은 두 번 눌러야 실행된다.
public sealed class DevPanel
{
    private const float ConfirmSeconds = 3f;
    private const int BackupsShown = 4;

    private readonly DevCommands commands;
    private readonly HitboxOverlay overlay;
    private readonly UIStateOverlay uiOverlay;

    private GUIStyle panelLabel, lineLabel, rowLabel, rowLabelLeft, button, warnButton, selectedButton, title, section;
    private int stylesForSize = -1;
    private Vector2 scroll;

    private int targetStage;
    private int targetLevel;
    private string pendingConfirm;
    private float pendingUntil;
    private IReadOnlyList<string> backupList = new List<string>();

    public DevPanel(DevCommands commands, HitboxOverlay overlay, UIStateOverlay uiOverlay)
    {
        this.commands = commands;
        this.overlay = overlay;
        this.uiOverlay = uiOverlay;
    }

    // 화면 짧은 변 360 기준의 1dp(화면 픽셀).
    public static float Unit => Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 360f);

    public void OnOpened()
    {
        targetStage = commands.CurrentStage;
        targetLevel = commands.Session.Stats.Level.Value;
        pendingConfirm = null;
        RefreshBackups();
    }

    // 닫혀 있을 때의 DEV 버튼(골드 표시 아래 오른쪽, 게임 UI가 없는 자리). 눌리면 true.
    public bool DrawToggleButton()
    {
        EnsureStyles();
        float u = Unit;
        Rect safe = SafeRect();
        var rect = new Rect(safe.xMax - 72f * u, Screen.height * 0.16f, 64f * u, 40f * u);

        GUI.color = new Color(1f, 1f, 1f, 0.7f);
        bool pressed = GUI.Button(rect, "DEV", button);
        GUI.color = Color.white;
        return pressed;
    }

    // 열린 패널. 닫기를 누르면 false.
    public bool DrawPanel()
    {
        EnsureStyles();
        float u = Unit;
        Rect safe = SafeRect();
        var area = new Rect(safe.x + 8f * u, safe.y + 8f * u, safe.width - 16f * u, Mathf.Min(safe.height * 0.8f, 700f * u));

        GUI.color = new Color(0f, 0f, 0f, 0.95f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = Color.white;

        bool keepOpen = true;
        GUILayout.BeginArea(new Rect(area.x + 8f * u, area.y + 8f * u, area.width - 16f * u, area.height - 16f * u));

        GUILayout.BeginHorizontal();
        GUILayout.Label("개발자 도구", title);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("닫기", button, GUILayout.Width(88f * u))) keepOpen = false;
        GUILayout.EndHorizontal();

        // 상태와 결과는 한 줄 고정 높이: 글이 길어져도 아래 버튼이 손가락 밑에서 움직이지 않게.
        GUILayout.Label(Status(), lineLabel);
        GUILayout.Label(string.IsNullOrEmpty(commands.LastMessage) ? " " : commands.LastMessage, lineLabel);

        scroll = GUILayout.BeginScrollView(scroll);
        DrawStage();
        DrawProgress();
        DrawTimeAndView();
        DrawSave(); // 백업 목록은 길이가 바뀌므로 맨 아래(위의 버튼 자리를 밀지 않게)
        GUILayout.EndScrollView();

        GUILayout.EndArea();
        return keepOpen;
    }

    // ── 묶음 ──

    private void DrawStage()
    {
        GUILayout.Label("스테이지", section);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", button) && StageExists(targetStage - 1)) targetStage--;
        GUILayout.Label($"스테이지 {targetStage}", rowLabel, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(">", button) && StageExists(targetStage + 1)) targetStage++;
        if (GUILayout.Button("이동", button)) commands.GoToStage(targetStage);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("지금 스테이지 처음부터", button)) commands.RestartStage();
        if (GUILayout.Button("보스 도전", button)) commands.ChallengeBoss();
        GUILayout.EndHorizontal();
    }

    private void DrawProgress()
    {
        GUILayout.Label("진행도", section);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("골드 x10", button)) commands.MultiplyGold();
        if (GUILayout.Button("경험치 +1레벨분", button)) commands.AddExpForOneLevel();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", button)) targetLevel = Mathf.Max(1, targetLevel - 1);
        GUILayout.Label($"레벨 {targetLevel}", rowLabel, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(">", button)) targetLevel = Mathf.Min(commands.MaxLevel, targetLevel + 1);
        if (ConfirmButton("level", "레벨 지정") && commands.SetLevel(targetLevel)) RefreshBackups();
        GUILayout.EndHorizontal();
    }

    private void DrawSave()
    {
        GUILayout.Label("세이브 (바꾸기 전에 자동 백업)", section);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("지금 백업", button) && commands.Backup()) RefreshBackups();
        if (ConfirmButton("reset", "새로 시작") && commands.StartOver()) RefreshBackups();
        GUILayout.EndHorizontal();

        if (backupList.Count == 0) GUILayout.Label("백업 없음", lineLabel);
        for (int i = 0; i < backupList.Count && i < BackupsShown; i++)
        {
            string path = backupList[i];
            GUILayout.BeginHorizontal();
            GUILayout.Label(Path.GetFileNameWithoutExtension(path), rowLabelLeft, GUILayout.ExpandWidth(true));
            if (ConfirmButton("restore:" + path, "복원") && commands.Restore(path)) RefreshBackups();
            GUILayout.EndHorizontal();
        }
    }

    private void DrawTimeAndView()
    {
        GUILayout.Label("시간 · 보기", section);

        GUILayout.BeginHorizontal();
        foreach (float scale in DevCommands.TimeScales)
        {
            bool current = Mathf.Approximately(Time.timeScale, scale);
            if (GUILayout.Button($"x{scale:0.#}", current ? selectedButton : button)) commands.TimeScale = scale;
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button(overlay.Enabled ? "판정 몸 표시: 켬" : "판정 몸 표시: 끔", overlay.Enabled ? selectedButton : button))
            overlay.Enabled = !overlay.Enabled;

        if (GUILayout.Button(uiOverlay.Enabled ? "UI 상태 표시: 켬" : "UI 상태 표시: 끔", uiOverlay.Enabled ? selectedButton : button))
            uiOverlay.Enabled = !uiOverlay.Enabled;
    }

    // ── 도우미 ──

    private string Status()
    {
        PlayerStats stats = commands.Session.Stats;
        string busy = commands.IsBusy ? " · 전환 중" : "";
        return $"스테이지 {commands.CurrentStage} · Lv {stats.Level.Value} · 골드 {NumberFormatUtility.Format(commands.Session.Wallet.Gold.Value)} · x{Time.timeScale:0.#}{busy}";
    }

    // 첫 번째로 누르면 확인 표시만 하고, ConfirmSeconds 안에 한 번 더 누르면 true.
    private bool ConfirmButton(string key, string label)
    {
        bool pending = pendingConfirm == key && Time.unscaledTime < pendingUntil;
        if (!GUILayout.Button(pending ? "한 번 더 누르면 실행" : label, pending ? warnButton : button)) return false;

        if (pending)
        {
            pendingConfirm = null;
            return true;
        }

        pendingConfirm = key;
        pendingUntil = Time.unscaledTime + ConfirmSeconds;
        return false;
    }

    private void RefreshBackups() => backupList = commands.ListBackups();

    private static bool StageExists(int stageNumber) =>
        stageNumber >= 1 && GameDatabaseManager.Instance != null && GameDatabaseManager.Instance.GetStage(stageNumber)?.data != null;

    // 노치·상태 표시줄을 피한 영역(GUI 좌표, 왼쪽 위 원점).
    private static Rect SafeRect()
    {
        Rect s = Screen.safeArea;
        return new Rect(s.x, Screen.height - s.yMax, s.width, s.height);
    }

    // GUI.skin은 OnGUI 안에서만 쓸 수 있어서 여기서 만든다. 화면 크기가 바뀌면 다시 만든다.
    private void EnsureStyles()
    {
        int size = Screen.width * 10000 + Screen.height;
        if (stylesForSize == size) return;
        stylesForSize = size;

        float u = Unit;
        int Font(float dp) => Mathf.RoundToInt(dp * u);

        panelLabel = new GUIStyle(GUI.skin.label) { fontSize = Font(14), wordWrap = true, alignment = TextAnchor.MiddleLeft };
        panelLabel.normal.textColor = Color.white;

        // 한 줄 고정 높이(넘치면 자른다)
        lineLabel = new GUIStyle(panelLabel) { wordWrap = false, clipping = TextClipping.Clip, fixedHeight = 22f * u };
        // 버튼 줄 안의 글: 버튼과 같은 높이, 가운데
        rowLabel = new GUIStyle(panelLabel) { wordWrap = false, alignment = TextAnchor.MiddleCenter, fixedHeight = 44f * u };
        rowLabelLeft = new GUIStyle(rowLabel) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };

        title = new GUIStyle(panelLabel) { fontSize = Font(18), fontStyle = FontStyle.Bold, wordWrap = false };
        section = new GUIStyle(panelLabel) { fontSize = Font(13), fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, Font(10), Font(2)) };
        section.normal.textColor = new Color(1f, 0.85f, 0.4f);

        button = new GUIStyle(GUI.skin.button) { fontSize = Font(15), fixedHeight = 44f * u, padding = new RectOffset(Font(8), Font(8), Font(4), Font(4)) };
        selectedButton = new GUIStyle(button);
        selectedButton.normal.textColor = selectedButton.hover.textColor = new Color(0.4f, 1f, 0.4f);
        warnButton = new GUIStyle(button);
        warnButton.normal.textColor = warnButton.hover.textColor = new Color(1f, 0.5f, 0.3f);
    }
}
