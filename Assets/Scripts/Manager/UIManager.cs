using System;
using System.Collections.Generic;
using UnityEngine;

// 패널 등록/조회와 탭 스택을 관리한다. 1차 이관이 끝나면 사라진다 (docs/UI_DESIGN.md 9장).
// 팝업(창)은 여기가 아니라 UIRoot의 WindowManager가 관리한다. 새 팝업을 여기에 추가하지 말 것.
// 지금 맡은 것은 탭뿐이다. HUD는 전투 4-B에서 View + Presenter(UIRoot가 만든다)가 됐다.
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    private readonly Dictionary<Type, UIBase> uiDict = new();
    private readonly Dictionary<UITabType, UITabPanel> tabDict = new();

    // 인덱스 0이 바닥, 마지막이 맨 위. StackMode가 Exclusive인 패널(탭)만 들어간다.
    private readonly List<UIBase> stack = new();

    // 스택 바닥이 탭이면 그 탭. 별도 상태를 두지 않고 계산해서 스택과 어긋날 일이 없다.
    public UITabType CurrentTab => stack.Count > 0 && stack[0] is UITabPanel tab ? tab.TabType : UITabType.None;

    public event Action<UITabType> OnTabChanged;

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

            ui.Hide();
            ui.Initialize();
        }
    }

    public void ShowUI<T>(Action<T> onShow = null) where T : UIBase
    {
        if (!uiDict.TryGetValue(typeof(T), out UIBase baseUI) || baseUI is not T ui)
        {
            Debug.LogError($"[UIManager] {typeof(T).Name} 패널을 찾을 수 없습니다!");
            return;
        }

        switch (ui.StackMode)
        {
            case StackMode.None:
                onShow?.Invoke(ui);
                ui.Show();
                break;

            case StackMode.Exclusive:
                onShow?.Invoke(ui);
                ShowExclusive(ui);
                break;
        }
    }

    public void HideUI<T>() where T : UIBase
    {
        if (!uiDict.TryGetValue(typeof(T), out UIBase ui)) return;

        // 바닥(탭)이 빠지면 CurrentTab이 바뀔 수 있어, 제거 전후를 비교해 알린다.
        UITabType prevTab = CurrentTab;
        stack.Remove(ui);
        ui.Hide();
        NotifyTabIfChanged(prevTab);
    }

    // 같은 탭을 다시 누르면 닫히고(토글), 다른 탭을 누르면 스택을 비우고 새 탭이 바닥이 된다.
    public void ToggleTab(UITabType tabType)
    {
        if (!tabDict.TryGetValue(tabType, out UITabPanel tabPanel))
        {
            Debug.LogError($"[UIManager] 탭 UI를 찾을 수 없습니다: {tabType}");
            return;
        }

        if (CurrentTab == tabType)
        {
            CloseAll();
            return;
        }

        ShowExclusive(tabPanel);
    }

    // 맨 위 패널 하나를 닫는다. 닫을 게 없으면 false.
    public bool Back()
    {
        if (stack.Count == 0) return false;

        UITabType prevTab = CurrentTab;
        UIBase top = stack[stack.Count - 1];
        stack.RemoveAt(stack.Count - 1);
        top.Hide();
        NotifyTabIfChanged(prevTab);
        return true;
    }

    // 스택을 전부 비운다.
    public void CloseAll()
    {
        UITabType prevTab = CurrentTab;
        CloseAbove(-1);
        NotifyTabIfChanged(prevTab);
    }

    // Exclusive 패널을 연다: 이미 바닥이면 그 위만 닫고, 아니면 스택을 비우고 바닥으로 만든다.
    private void ShowExclusive(UIBase ui)
    {
        UITabType prevTab = CurrentTab;

        if (stack.Count > 0 && stack[0] == ui)
        {
            CloseAbove(0);
        }
        else
        {
            CloseAbove(-1);
            stack.Add(ui);
            ui.Show();
        }

        NotifyTabIfChanged(prevTab);
    }

    // index 위의 패널을 위에서부터 닫는다. -1이면 바닥까지 전부.
    private void CloseAbove(int index)
    {
        for (int i = stack.Count - 1; i > index; i--)
        {
            UIBase ui = stack[i];
            stack.RemoveAt(i);
            ui.Hide();
        }
    }

    private void NotifyTabIfChanged(UITabType prevTab)
    {
        UITabType now = CurrentTab;
        if (now != prevTab) OnTabChanged?.Invoke(now);
    }
}
