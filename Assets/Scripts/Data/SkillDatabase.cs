using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Data/Skill Database")]
public class SkillDatabase : ScriptableObject
{
    public List<SkillData> entries = new List<SkillData>();

    private Dictionary<int, SkillData> lookup;

    public SkillData GetById(int id)
    {
        EnsureLookup();
        return lookup.TryGetValue(id, out var entry) ? entry : null;
    }

    public IReadOnlyList<SkillData> GetAll()
    {
        return entries;
    }

    public void EnsureLookup()
    {
        if (lookup != null) return;

        lookup = new Dictionary<int, SkillData>();
        foreach (var e in entries)
        {
            if (e == null) continue;
            lookup[e.id] = e;
        }
    }

    private void OnValidate()
    {
        lookup = null; // 인스펙터에서 직접 수정하면 캐시 무효화
    }
}
