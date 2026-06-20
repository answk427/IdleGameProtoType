using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StageDatabase", menuName = "Data/Stage Database")]
public class StageDatabase : ScriptableObject
{
    public List<StageEntry> entries = new List<StageEntry>();

    private Dictionary<int, StageEntry> lookup;

    public StageEntry GetByNumber(int stageNumber)
    {
        EnsureLookup();
        return lookup.TryGetValue(stageNumber, out var entry) ? entry : null;
    }

    public void EnsureLookup()
    {
        if (lookup != null) return;

        lookup = new Dictionary<int, StageEntry>();
        foreach (var e in entries)
        {
            if (e == null) continue;
            lookup[e.data.stageNumber] = e;
        }
    }

    private void OnValidate()
    {
        lookup = null;
    }
}
