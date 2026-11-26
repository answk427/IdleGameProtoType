using System.Text;
using UnityEngine;

// UI 상태 표시(docs/DEV_TOOLS.md T8, docs/UI_DESIGN.md 10-6): 창 스택(맨 위부터), 공용 딤의 자리, 입력 차단을 쥔 이유와
// 지난 시간, 지금 토스트. "버튼이 안 눌린다", "뒤로가기가 엉뚱한 창을 닫는다", "입력이 영구히 잠겼다"를 볼 때 쓴다.
//
// 게임 쪽에서는 읽기 전용 사본(UIRoot.Describe)만 받는다. 판정 몸 표시(HitboxOverlay)와 같은 모양이다.
public sealed class UIStateOverlay
{
    private readonly UIRoot uiRoot;
    private readonly UIDebugSnapshot snapshot = new UIDebugSnapshot();
    private readonly StringBuilder text = new StringBuilder();
    private GUIStyle style;

    public UIStateOverlay(UIRoot uiRoot)
    {
        this.uiRoot = uiRoot;
    }

    public bool Enabled { get; set; }

    public void Draw(float unit)
    {
        if (!Enabled || uiRoot == null || Event.current.type != EventType.Repaint) return;

        uiRoot.Describe(snapshot);
        BuildText();

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = false, richText = false };
            style.normal.textColor = Color.white;
        }
        style.fontSize = Mathf.RoundToInt(10f * unit);
        style.padding = new RectOffset(Mathf.RoundToInt(6f * unit), Mathf.RoundToInt(6f * unit), Mathf.RoundToInt(4f * unit), Mathf.RoundToInt(4f * unit));

        var content = new GUIContent(text.ToString());
        Vector2 size = style.CalcSize(content);
        // 왼쪽, 화면 위쪽 22% 아래(HUD를 가리지 않는 자리). 터치를 받지 않는다(그리기만).
        var rect = new Rect(4f * unit, Screen.height * 0.22f, size.x, size.y);

        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUI.Box(rect, content, style);
        GUI.color = Color.white;
    }

    private void BuildText()
    {
        text.Clear();
        text.Append("UI 상태\n창 스택 (위 → 아래)");

        if (snapshot.Windows.Count == 0) text.Append("\n  (없음)");
        for (int i = snapshot.Windows.Count - 1; i >= 0; i--)
        {
            UIDebugSnapshot.WindowInfo w = snapshot.Windows[i];
            text.Append("\n  ").Append(w.Name);
            if (!w.IsLoaded)
            {
                text.Append("  로드 중");
                continue;
            }

            text.Append("  ").Append(w.Band).Append(' ').Append(w.Order);
            text.Append(w.IsOpen ? "  열림" : "  여는 중");
            if (w.Modal) text.Append("  모달");
        }

        text.Append("\n딤: ").Append(snapshot.DimOrder.HasValue ? snapshot.DimOrder.Value.ToString() : "없음");

        text.Append("\n입력 차단: ");
        if (snapshot.Blocks.Count == 0) text.Append("없음");
        foreach (UIDebugSnapshot.BlockInfo block in snapshot.Blocks)
        {
            text.Append("\n  ").Append(block.Reason).Append(" (").Append(block.Seconds.ToString("0.0")).Append("초)");
        }

        text.Append("\n토스트: ").Append(snapshot.Toast ?? "없음");
    }
}
