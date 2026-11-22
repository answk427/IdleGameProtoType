using System;
using UnityEngine;
using UnityEngine.UI;

// 공용 딤 1장. 맨 위 모달 바로 아래(-1칸)에 놓여 그 밑의 터치를 막는다.
// 창마다 두면 겹칠 때마다 점점 까매진다.
internal sealed class WindowDim
{
    private readonly Canvas canvas;
    private readonly GraphicRaycaster raycaster;
    private readonly Image image;

    public WindowDim(Transform uiRoot, Action onClicked)
    {
        canvas = UILayerFactory.CreateLayer("Dim", uiRoot, UISort.DimOrder((int)UISortBand.Popup));
        raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        image = UILayerFactory.CreateFullScreenImage("DimImage", canvas.transform, Color.clear);

        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => onClicked());

        Hide();
    }

    public void Show(int order, Color color)
    {
        if (canvas == null) return;

        canvas.enabled = true;
        raycaster.enabled = true;
        UISort.Apply(canvas, order);
        image.color = color;
    }

    public void Hide()
    {
        if (canvas == null) return;

        canvas.enabled = false;
        raycaster.enabled = false;
    }
}
