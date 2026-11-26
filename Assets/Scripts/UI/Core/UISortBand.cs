using UnityEngine;

// 화면 UI의 그리는 순서 = sortingOrder 숫자의 구간 (docs/UI_DESIGN.md 4-1).
// Screen Space - Overlay에는 Sorting Layer가 적용되지 않아서 구간으로만 순서를 정한다.
public enum UISortBand
{
    Fx = 0,
    Hud = 1000,
    Window = 2000,
    Popup = 3000,
    Toast = 4000, // 창이 아니다(스택 밖, 터치를 받지 않음). UIRoot의 토스트 층 하나
    System = 9000,
}

public static class UISort
{
    // 창 하나가 쓰는 칸: -1 공용 딤, 0 본체, +1~+8 창 안에서 위에 떠야 하는 요소.
    private const int OrdersPerWindow = 10;
    private const int DepthsPerBand = 100;

    // System 구분의 맨 위 두 깊이는 창이 아닌 것의 고정 자리다.
    private const int FadeDepth = DepthsPerBand - 2;
    private const int InputBlockDepth = DepthsPerBand - 1;

    public const int FadeOrder = (int)UISortBand.System + FadeDepth * OrdersPerWindow;
    public const int InputBlockOrder = (int)UISortBand.System + InputBlockDepth * OrdersPerWindow;

    // 한도를 넘으면 다른 구분을 조용히 침범하지 않고 한도에서 멈추고 에러를 낸다.
    public static int WindowOrder(UISortBand band, int depth)
    {
        int limit = band == UISortBand.System ? FadeDepth : DepthsPerBand;
        if (depth >= limit)
        {
            Debug.LogError($"[UISort] {band} 구분의 깊이 한도({limit})를 넘었습니다.");
            depth = limit - 1;
        }

        return (int)band + depth * OrdersPerWindow;
    }

    public static int DimOrder(int windowOrder) => windowOrder - 1;

    // overrideSorting은 비활성 상태에서 넣으면 무시되므로, 프리팹에 저장해 두지 않고 활성 상태에서 코드가 넣는다.
    public static void Apply(Canvas canvas, int order)
    {
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
    }
}
