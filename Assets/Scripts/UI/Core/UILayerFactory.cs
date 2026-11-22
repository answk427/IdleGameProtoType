using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// UIRoot 밑에 자기 Canvas를 가진 전체 화면 칸(창, 딤, 입력 차단막)을 만든다.
internal static class UILayerFactory
{
    public static Canvas CreateLayer(string name, Transform root, int order)
    {
        var layer = new GameObject(name, typeof(RectTransform)) { layer = root.gameObject.layer };
        layer.transform.SetParent(root, false);
        Stretch((RectTransform)layer.transform);

        Canvas canvas = layer.AddComponent<Canvas>();
        UISort.Apply(canvas, order);

        // TMP가 쓰는 정점 채널. 중첩 Canvas는 루트의 설정을 물려받지 않는다.
        if (root.TryGetComponent(out Canvas rootCanvas))
        {
            canvas.additionalShaderChannels = rootCanvas.additionalShaderChannels;
        }

        return canvas;
    }

    public static Image CreateFullScreenImage(string name, Transform parent, Color color)
    {
        var child = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
        child.transform.SetParent(parent, false);
        Stretch((RectTransform)child.transform);

        Image image = child.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static void ClearSelection()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
