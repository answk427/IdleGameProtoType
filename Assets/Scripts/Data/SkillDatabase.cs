using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Data/Skill Database")]
public class SkillDatabase : ScriptableObject
{
    public List<SkillEntry> entries = new List<SkillEntry>();

    private Dictionary<int, SkillEntry> lookup;

    public SkillEntry GetById(int id)
    {
        EnsureLookup();
        return lookup.TryGetValue(id, out var entry) ? entry : null;
    }

    public IReadOnlyList<SkillEntry> GetAll()
    {
        return entries;
    }

    public void EnsureLookup()
    {
        if (lookup != null) return;

        lookup = new Dictionary<int, SkillEntry>();
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
}
