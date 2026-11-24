using System.Collections.Generic;
using UnityEngine;

// 캐릭터 하나의 행동 시간표 목록(docs/COMBAT_ARCHITECTURE.md 7-2). 캐릭터 프리팹이 참조한다.
// 타이밍은 스킬이 아니라 시전자의 애니메이션에 속한다: 같은 스킬도 시전자마다 다른 클립을 쓴다.
[CreateAssetMenu(menuName = "IdleGame/Combat/Action Timeline Set", fileName = "ActionTimelineSet")]
public class ActionTimelineSet : ScriptableObject, IActionTimelineSource
{
    [SerializeField] private ActionTimeline[] timelines = new ActionTimeline[0];

    public IReadOnlyList<ActionTimeline> Timelines => timelines;

    public bool TryGet(string key, out ActionTimeline timeline)
    {
        for (int i = 0; i < timelines.Length; i++)
        {
            if (timelines[i] != null && timelines[i].Key == key)
            {
                timeline = timelines[i];
                return true;
            }
        }

        timeline = null;
        return false;
    }
}
