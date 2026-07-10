using System;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    // 팝업형 패널(가끔 떴다 사라지는 UI)은 씬에 미리 배치하지 않고, 이 레지스트리에
    // 등록된 프리팹을 첫 ShowUI 시점에 지연 생성한다
    // 상시형 패널(PlayerHud, 탭 패널)은 기존대로 씬에 배치하면 Awake 스캔으로 등록된다.
    [SerializeField] private UIRegistry registry;

    [Header("레이어별 패널 부모 (지연 생성 시 어느 캔버스 밑에 붙일지)")]
    [SerializeField] private Transform staticRoot;
    [SerializeField] private Transform dynamicRoot;
    [SerializeField] private Transform topRoot;

    private readonly Dictionary<Type, UIBase> uiDict = new();

    // 레지스트리에서 빌드되는 타입 → 프리팹 매핑 (지연 생성용)
    private readonly Dictionary<Type, UIBase> prefabDict = new();

    // 그룹 제어용 Dictionary
    private readonly Dictionary<UILayer, List<UIBase>> layerGroup = new()
    {
        { UILayer.Static,  new List<UIBase>() },
        { UILayer.Dynamic, new List<UIBase>() },
        { UILayer.Top,     new List<UIBase>() }
    };

    private readonly Dictionary<UITabType, UITabPanel> tabDict = new();
    public UITabType CurrentTab { get; private set; } = UITabType.None;

    private void Awake()
    {
        // 씬 이동 시 겹침 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 씬에 배치된 상시형 패널 등록
        UIBase[] uis = GetComponentsInChildren<UIBase>(true);
        foreach (UIBase ui in uis)
        {
            if (Register(ui))
            {
                ui.Hide();
            }
        }

        BuildPrefabDict();
    }

    // 씬 스캔/지연 생성 두 경로가 공유하는 단일 등록 지점.
    private bool Register(UIBase ui)
    {
        Type type = ui.GetType();

        if (uiDict.ContainsKey(type))
        {
            Debug.LogWarning($"[UIManager] 중복 등록 시도: {type.Name}");
            return false;
        }

        uiDict.Add(type, ui);
        layerGroup[ui.Layer].Add(ui);

        if (ui is UITabPanel tabPanel && tabPanel.TabType != UITabType.None)
        {
            if (tabDict.ContainsKey(tabPanel.TabType))
            {
                Debug.LogWarning($"[UIManager] 탭 중복 등록: {tabPanel.TabType}");
            }
            else
            {
                tabDict.Add(tabPanel.TabType, tabPanel);
            }
        }

        return true;
    }

    private void BuildPrefabDict()
    {
        if (registry == null) return;

        foreach (UIBase prefab in registry.PanelPrefabs)
        {
            if (prefab == null) continue;

            Type type = prefab.GetType();
            if (prefabDict.ContainsKey(type))
            {
                // UIBase를 상속한 클래스는 1개만 사용 가능
                Debug.LogError($"[UIManager] UIRegistry에 같은 타입이 중복 등록됨: {type.Name}");
                continue;
            }

            prefabDict.Add(type, prefab);
        }
    }

    public void ShowUI<T>(Action<T> onShow = null) where T : UIBase
    {
        if (!uiDict.TryGetValue(typeof(T), out UIBase baseUI))
        {
            baseUI = InstantiatePanel(typeof(T));
            if (baseUI == null) return;
        }

        if (baseUI is T ui)
        {
            onShow?.Invoke(ui);
            ui.Show();
        }
    }

    // 레지스트리의 프리팹으로 패널을 지연 생성해서 등록한다. 첫 ShowUI 때 한 번만
    // 생성되고 이후에는 uiDict에 캐시된 인스턴스를 Show/Hide로 재사용한다.
    private UIBase InstantiatePanel(Type type)
    {
        if (!prefabDict.TryGetValue(type, out UIBase prefab))
        {
            Debug.LogError($"[UIManager] {type.Name} 패널이 없습니다 — 씬 배치도, UIRegistry 등록도 안 됨.");
            return null;
        }

        UIBase ui = Instantiate(prefab, GetLayerRoot(prefab.Layer));
        Register(ui);
        return ui;
    }

    private Transform GetLayerRoot(UILayer layer)
    {
        Transform root = layer switch
        {
            UILayer.Dynamic => dynamicRoot,
            UILayer.Top => topRoot,
            _ => staticRoot,
        };

        if (root == null)
        {
            Debug.LogWarning($"[UIManager] {layer} 레이어 루트가 설정되지 않아 UIManager 밑에 생성합니다.");
            return transform;
        }

        return root;
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


    // 같은 탭을 다시 누르면 닫히고(토글), 다른 탭을 누르면 이전 탭은 닫히고 새 탭이 열림.
    public void ToggleTab(UITabType tabType)
    {
        if (CurrentTab == tabType)
        {
            CloseCurrentTab();
            return;
        }

        if (CurrentTab != UITabType.None && tabDict.TryGetValue(CurrentTab, out UITabPanel prevUi))
        {
            prevUi.Hide();
        }

        if (tabDict.TryGetValue(tabType, out UITabPanel nextUi))
        {
            nextUi.Show();
            CurrentTab = tabType;
        }
        else
        {
            Debug.LogError($"[UIManager] 탭 UI를 찾을 수 없습니다: {tabType}");
            CurrentTab = UITabType.None;
        }
    }

    public void CloseCurrentTab()
    {
        if (CurrentTab == UITabType.None) return;

        if (tabDict.TryGetValue(CurrentTab, out UITabPanel ui))
        {
            ui.Hide();
        }
        CurrentTab = UITabType.None;
    }
}
