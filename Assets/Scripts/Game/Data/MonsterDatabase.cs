using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MonsterDatabase", menuName = "Data/Monster Database")]
public class MonsterDatabase : ScriptableObject, IDataTable
{
    public List<MonsterEntry> entries = new List<MonsterEntry>();

    private Dictionary<int, MonsterEntry> lookup;

    public MonsterEntry GetById(int id)
    {
        EnsureLookup();
        return lookup.TryGetValue(id, out var entry) ? entry : null;
    }

    public void EnsureLookup()
    {
        if (lookup != null) return;

        lookup = new Dictionary<int, MonsterEntry>();
        foreach (var e in entries)
        {
            if (e?.data == null) continue;
            lookup[e.data.Id] = e;
        }
    }

    private void OnValidate()
    {
        lookup = null; // 인스펙터에서 직접 수정하면 캐시 무효화
    }

    Type IDataTable.DataType => typeof(MonsterData);
    int IDataTable.RowCount => entries.Count;
    IData IDataTable.GetRow(int index) => entries[index]?.data;
}
