using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 창 하나의 Canvas. 프리팹은 내용만 담고, 정렬과 숨김에 필요한 것은 여기서 붙인다.
internal sealed class WindowHost
{
    private readonly GameObject root;
    private readonly Canvas canvas;
    private readonly CanvasGroup group;

    // 숨길 때 켜져 있던 것만 기억했다가 되살린다(원래 꺼져 있던 자식은 그대로 둔다).
    private readonly List<Canvas> disabledCanvases = new List<Canvas>();
    private readonly List<GraphicRaycaster> disabledRaycasters = new List<GraphicRaycaster>();
    private bool isHidden;

    public UIWindow View { get; }
    public int Order => canvas.sortingOrder;
    public bool IsAlive => root != null;

    public WindowHost(Transform uiRoot, UIWindow prefab)
    {
        canvas = UILayerFactory.CreateLayer(prefab.GetType().Name, uiRoot, (int)prefab.Options.Band);
        root = canvas.gameObject;
        root.AddComponent<GraphicRaycaster>();
        group = root.AddComponent<CanvasGroup>();

        try
        {
            View = Object.Instantiate(prefab, root.transform, false);
            View.gameObject.SetActive(true);
            View.NotifyInitialized();
        }
        catch
        {
            Object.Destroy(root);
            throw;
        }
    }

    public void SetOrder(int order) => UISort.Apply(canvas, order);

    public void Show()
    {
        if (!isHidden) return;
        isHidden = false;

        foreach (Canvas item in disabledCanvases)
        {
            if (item != null) item.enabled = true;
        }
        foreach (GraphicRaycaster item in disabledRaycasters)
        {
            if (item != null) item.enabled = true;
        }
        disabledCanvases.Clear();
        disabledRaycasters.Clear();

        group.interactable = true;
    }

    // SetActive(false) 대신 그리기와 입력만 끈다(재표시 때 OnEnable, 레이아웃 재계산이 없다).
    // 자식 Canvas는 부모를 꺼도 터치를 받고, 안 보이는 버튼도 Submit에는 눌리므로 셋 다 꺼야 한다.
    public void Hide()
    {
        if (isHidden || !IsAlive) return;
        isHidden = true;

        // 자식(+1~+8칸)부터 꺼지고 본체가 마지막이 되게 역순으로 끈다.
        Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
        for (int i = canvases.Length - 1; i >= 0; i--)
        {
            if (!canvases[i].enabled) continue;
            canvases[i].enabled = false;
            disabledCanvases.Add(canvases[i]);
        }

        foreach (GraphicRaycaster raycaster in root.GetComponentsInChildren<GraphicRaycaster>(true))
        {
            if (!raycaster.enabled) continue;
            raycaster.enabled = false;
            disabledRaycasters.Add(raycaster);
        }

        group.interactable = false;

        EventSystem events = EventSystem.current;
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        if (selected != null && selected.transform.IsChildOf(root.transform)) UILayerFactory.ClearSelection();
    }
}
