using UnityEngine;

// 판정 몸 표시(docs/DEV_TOOLS.md T7). 전투원마다 판정 몸(중심 ± 반너비)과 사거리 경계(몸 끝 + 사거리)를 그린다.
//
// 위치는 시뮬레이션의 X(판정이 쓰는 값)다. 캐릭터 그림은 직전 두 단계 사이로 보간돼 최대 한 단계(0.02초) 늦게
// 보이므로, 달리는 중에는 상자가 그림보다 조금 앞설 수 있다. 서 있을 때는 같은 자리다.
// 판정 몸에는 높이가 없다. 상자의 높이는 보기 위한 것이다.
public sealed class HitboxOverlay
{
    private const float BodyHeight = 1.2f;   // 월드 유닛
    private const float Line = 2f;           // 화면 픽셀

    private static readonly Color AllyColor = new Color(0.2f, 1f, 0.3f);
    private static readonly Color EnemyColor = new Color(1f, 0.3f, 0.3f);
    private static readonly Color DeadColor = new Color(0.6f, 0.6f, 0.6f);

    private readonly CombatWorld world;
    private GUIStyle labelStyle;

    public HitboxOverlay(CombatWorld world)
    {
        this.world = world;
    }

    public bool Enabled { get; set; }

    public void Draw(Camera camera, float unit)
    {
        if (!Enabled || world == null || camera == null || Event.current.type != EventType.Repaint) return;

        if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = Mathf.RoundToInt(11f * unit);

        foreach (Combatant c in world.Combatants)
        {
            if (c.Phase == CombatantPhase.Inactive) continue;

            // 아래 변은 캐릭터가 서는 바닥(PlayAreaBounds.GroundY). 피벗은 발이 아닐 수 있다.
            float baseY = PlayAreaBounds.Instance != null ? PlayAreaBounds.Instance.GroundY
                : c.Host is Component host ? host.transform.position.y : 0f;
            Color color = !c.IsAlive ? DeadColor : c.Team == Team.Ally ? AllyColor : EnemyColor;

            // 몸
            Rect body = WorldRect(camera, c.X - c.HalfWidth, c.X + c.HalfWidth, baseY, baseY + BodyHeight);
            Fill(body, new Color(color.r, color.g, color.b, 0.2f));
            Outline(body, color);

            // 몸 중심
            Rect center = WorldRect(camera, c.X, c.X, baseY, baseY + BodyHeight);
            Fill(new Rect(center.x - Line * 0.5f, center.y, Line, center.height), color);

            // 사거리 경계: 표면 거리라 몸 끝에서 사거리만큼. 대상은 앞뒤 무관이라 양쪽에 그린다.
            float reach = c.HalfWidth + c.AttackRange;
            Rect range = WorldRect(camera, c.X - reach, c.X + reach, baseY, baseY + BodyHeight * 0.4f);
            Fill(new Rect(range.xMin, range.y, Line, range.height), color);
            Fill(new Rect(range.xMax - Line, range.y, Line, range.height), color);
            Fill(new Rect(range.xMin, range.yMax - Line, range.width, Line), new Color(color.r, color.g, color.b, 0.5f));

            GUI.color = color;
            GUI.Label(new Rect(body.x, body.y - 18f * unit, 200f * unit, 18f * unit),
                $"{c.Phase} HP {NumberFormatUtility.Format(c.Hp)}", labelStyle);
            GUI.color = Color.white;
        }
    }

    // 월드 사각형(x 구간, y 구간) → GUI 좌표(왼쪽 위 원점)
    private static Rect WorldRect(Camera camera, float xMin, float xMax, float yMin, float yMax)
    {
        Vector3 a = camera.WorldToScreenPoint(new Vector3(xMin, yMin, 0f));
        Vector3 b = camera.WorldToScreenPoint(new Vector3(xMax, yMax, 0f));
        return Rect.MinMaxRect(a.x, Screen.height - b.y, b.x, Screen.height - a.y);
    }

    private static void Fill(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private static void Outline(Rect r, Color color)
    {
        Fill(new Rect(r.xMin, r.yMin, r.width, Line), color);
        Fill(new Rect(r.xMin, r.yMax - Line, r.width, Line), color);
        Fill(new Rect(r.xMin, r.yMin, Line, r.height), color);
        Fill(new Rect(r.xMax - Line, r.yMin, Line, r.height), color);
    }
}
