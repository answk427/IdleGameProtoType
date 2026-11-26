using System;
using System.Collections.Generic;

// Presenter가 아는 탭바의 전부. uGUI 타입이 없다.
public interface ITabBarView
{
    event Action<UITabType> TabTapped;

    // 탭바에 있는 탭(버튼 순서).
    IReadOnlyList<UITabType> Tabs { get; }

    void SetLocked(UITabType tab, bool locked);

    // 선택된 탭 표시. None이면 아무것도 선택되지 않은 모양.
    void SetSelected(UITabType tab);

    // 레드닷(할 일이 있음).
    void SetBadge(UITabType tab, bool visible);
}
