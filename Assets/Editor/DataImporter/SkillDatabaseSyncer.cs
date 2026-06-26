using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// SkillData → SkillDatabase(SO) 동기화 담당.
// icon/vfx/sfx 같은 참조 필드는 자동 매칭하지 않고, 기존에 연결되어 있던 값을 그대로 보존한다.
// (디자이너가 Inspector에서 수동으로 연결한 참조가 엑셀 재변환 때마다 날아가지 않도록.)
// ExcelToJsonConverter는 이 클래스를 IDataSyncer 구현체로 리플렉션 탐색해 자동으로 실행한다.
public class SkillDatabaseSyncer : IDataSyncer
{
    private const string DbPath = "Assets/Data/Skill/SkillDatabase.asset";

    public Type DataType => typeof(SkillData);

    public void Sync(IList dataList, Action<string> log)
    {
        var db = DatabaseSyncUtility.LoadOrCreateDatabaseAsset<SkillDatabase>(DbPath, log);

        // 기존 entry를 id 기준으로 백업 (참조 필드 보존용)
        var existingById = new Dictionary<int, SkillEntry>();
        foreach (var e in db.entries)
        {
            if (e?.data != null) existingById[e.data.Id] = e;
        }

        var newEntries = new List<SkillEntry>();
        int preserved = 0;
        int created = 0;

        foreach (SkillData data in dataList)
        {
            if (existingById.TryGetValue(data.Id, out var existing))
            {
                // 참조 필드(icon, vfx, sfx)는 그대로 유지하고 data만 최신화
                existing.data = data;
                newEntries.Add(existing);
                preserved++;
            }
            else
            {
                // 신규 스킬은 참조 필드가 빈 채로 추가됨 (디자이너가 직접 연결 필요)
                newEntries.Add(new SkillEntry { data = data });
                created++;
            }
        }
        newEntries.Sort((a, b) => a.data.Id.CompareTo(b.data.Id));

        db.entries = newEntries;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        log?.Invoke($"\u2705 SkillDatabase 동기화 완료: {newEntries.Count}개 (참조 유지 {preserved}개, 신규 {created}개)");

        if (created > 0)
        {
            log?.Invoke($"[알림] 신규 스킬 {created}개는 icon/VFX/SFX가 비어있습니다. Inspector에서 직접 연결해주세요.");
        }
    }
}
