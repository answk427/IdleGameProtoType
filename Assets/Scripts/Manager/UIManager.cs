using System;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    private readonly Dictionary<Type, UIBase> uiDict = new();
    
    // 그룹 제어용 Dictionary
    private readonly Dictionary<UILayer, List<UIBase>> layerGroup = new()
    {
        { UILayer.Static,  new List<UIBase>() },
        { UILayer.Dynamic, new List<UIBase>() },
        { UILayer.Top,     new List<UIBase>() }
    };

    private void Awake()
    {
        // 씬 이동 시 겹침 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        UIBase[] uis = GetComponentsInChildren<UIBase>(true);

        foreach (UIBase ui in uis)
        {
            Type type = ui.GetType();

            if (uiDict.ContainsKey(type))
            {
                Debug.LogWarning($"[UIManager] 중복 등록 시도: {type.Name}");
                continue;
            }

            uiDict.Add(type, ui);
            layerGroup[ui.Layer].Add(ui);

            ui.Hide();
        }
    }

    public void ShowUI<T>(Action<T> onShow = null) where T : UIBase
    {
        if (uiDict.TryGetValue(typeof(T), out UIBase baseUI) && baseUI is T ui)
        {
            onShow?.Invoke(ui);
            ui.Show();
        }
        else
        {
            Debug.LogError($"[UIManager] {typeof(T).Name} 패널을 찾을 수 없습니다!");
        }
    }

    public void HideUI<T>() where T : UIBase
    {
        if (uiDict.TryGetValue(typeof(T), out UIBase baseUI))
        {
            baseUI.Hide();
        }
    }

    public void HideAllUI(UILayer layer)
    {
        foreach (UIBase ui in layerGroup[layer])
        {
            ui.Hide();
        }
    }
}