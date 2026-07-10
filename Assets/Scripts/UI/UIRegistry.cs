using System.Collections.Generic;
using UnityEngine;

// 런타임에 지연 생성되는 UI 패널(팝업형) 프리팹의 등록처.
// 항상 떠있는 상시형 패널(PlayerHud, 탭 패널)은 여기 등록하지 않고 씬에 배치한다.
[CreateAssetMenu(fileName = "UIRegistry", menuName = "Game/UI Registry")]
public class UIRegistry : ScriptableObject
{
    [SerializeField] private List<UIBase> panelPrefabs = new();

    public IReadOnlyList<UIBase> PanelPrefabs => panelPrefabs;
}
