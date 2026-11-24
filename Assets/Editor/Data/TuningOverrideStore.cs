using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 튜닝 덮어쓰기 목록(docs/COMBAT_ARCHITECTURE.md 8-7, D15). 내 PC 전용 파일(UserSettings/)에 저장해 저장소에 올라가지 않는다.
// 항목은 "표(데이터 클래스) / 키 / 열(필드) / 값"이고, 값은 엑셀 변환기와 같은 규칙(ExcelToJsonConverter.ConvertCell)으로 검사한다.
// 적용은 런타임의 DataTuning이 한다(에디터 플레이에서만, 데이터베이스 사본에).
[FilePath("UserSettings/IdleGame/TuningOverrides.asset", FilePathAttribute.Location.ProjectFolder)]
public sealed class TuningOverrideStore : ScriptableSingleton<TuningOverrideStore>
{
    [Serializable]
    public class Entry
    {
        public bool enabled = true;
        public string table = "";  // 데이터 클래스 이름(예: MonsterData)
        public int key;
        public string column = ""; // 필드 이름 = 엑셀 열 이름
        public string value = "";
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public List<Entry> Entries => entries;

    public void SaveNow() => Save(true);

    // ── 표 정보(데이터 클래스와 그 데이터베이스 에셋) ──

    private static List<Type> dataTypes;

    public static IReadOnlyList<Type> DataTypes => dataTypes ??= TypeCache.GetTypesDerivedFrom<IData>()
        .Where(t => t.IsClass && !t.IsAbstract)
        .OrderBy(t => t.Name)
        .ToList();

    public static Type FindType(string name) => DataTypes.FirstOrDefault(t => t.Name == name);

    public static FieldInfo FindField(Type type, string name) =>
        type == null ? null : ExcelToJsonConverter.SerializedFields(type).FirstOrDefault(f => f.Name == name);

    private static readonly Dictionary<Type, ScriptableObject> tableAssets = new Dictionary<Type, ScriptableObject>();

    // 그 데이터 클래스를 담은 데이터베이스 에셋(엑셀 시트 이름 = 에셋 이름). 창이 매 프레임 부르므로 찾은 것은 기억해 둔다.
    public static ScriptableObject FindTableAsset(Type dataType)
    {
        if (dataType == null) return null;
        if (tableAssets.TryGetValue(dataType, out ScriptableObject cached) && cached != null) return cached;

        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Data" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset is IDataTable table && table.DataType == dataType)
            {
                tableAssets[dataType] = asset;
                return asset;
            }
        }
        return null;
    }

    public static IData FindRow(IDataTable table, int key)
    {
        if (table == null) return null;
        for (int i = 0; i < table.RowCount; i++)
        {
            IData row = table.GetRow(i);
            if (row != null && row.Key == key) return row;
        }
        return null;
    }

    // ── 검사와 변환 ──

    // 항목 하나를 검사한다. 통과하면 적용할 값, 아니면 이유.
    public static bool TryResolve(Entry entry, out DataTuning.Override result, out string error)
    {
        result = default;

        Type type = FindType(entry.table);
        if (type == null) { error = $"표 '{entry.table}'이 없습니다"; return false; }

        FieldInfo field = FindField(type, entry.column);
        if (field == null) { error = $"{type.Name}에 열 '{entry.column}'이 없습니다"; return false; }

        IData row = FindRow(FindTableAsset(type) as IDataTable, entry.key);
        if (row == null) { error = $"{type.Name}에 키 {entry.key}인 행이 없습니다"; return false; }

        object value = ExcelToJsonConverter.ConvertCell(entry.value, field.FieldType);
        if (value == null) { error = $"'{entry.value}'은 {field.FieldType.Name} 형식이 아닙니다"; return false; }

        // 키 열은 덮어쓸 수 없다(행을 찾는 기준이 바뀐다). 사본에 써 보고 키가 바뀌는지 본다.
        var probe = (IData)JsonConvert.DeserializeObject(JsonConvert.SerializeObject(row), type);
        field.SetValue(probe, value);
        if (probe.Key != entry.key) { error = $"{field.Name}은 키 열이라 덮어쓸 수 없습니다"; return false; }

        result = new DataTuning.Override(type, entry.key, field, value);
        error = null;
        return true;
    }

    // 켜져 있고 검사를 통과한 항목. errors가 있으면 실패한 항목의 이유를 담는다.
    public List<DataTuning.Override> BuildActive(List<string> errors)
    {
        var active = new List<DataTuning.Override>();
        foreach (Entry entry in entries)
        {
            if (!entry.enabled) continue;
            if (TryResolve(entry, out DataTuning.Override resolved, out string error)) active.Add(resolved);
            else errors?.Add($"{entry.table} {entry.key} {entry.column} = '{entry.value}': {error}");
        }
        return active;
    }

    public int EnabledCount => entries.Count(e => e.enabled);
}

// 런타임 DataTuning에 목록을 넘기고, 플레이 시작 때 켜진 덮어쓰기를 경고로 알린다(세이브 프로필 경고와 같은 방식).
[InitializeOnLoad]
internal static class TuningOverrideBridge
{
    static TuningOverrideBridge()
    {
        DataTuning.Provider = () => TuningOverrideStore.instance.BuildActive(null);
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode) ReportActive();
        };
    }

    private static void ReportActive()
    {
        var errors = new List<string>();
        List<DataTuning.Override> active = TuningOverrideStore.instance.BuildActive(errors);

        foreach (string error in errors) Debug.LogError($"[Tuning] 적용하지 않은 덮어쓰기: {error}");
        if (active.Count == 0) return;

        var lines = active.Select(o => $"  {o.DataType.Name} {o.Key} {o.Field.Name} = {o.Value}");
        Debug.LogWarning($"[Tuning] 튜닝 덮어쓰기 {active.Count}개 사용 중(엑셀·.asset과 다름, 빌드에는 반영 안 됨):\n{string.Join("\n", lines)}");
    }
}

// 빌드에는 덮어쓰기가 들어가지 않는다. 남아 있으면 엑셀에 옮겼는지 확인하라고 알린다(빌드는 막지 않는다).
internal sealed class TuningOverrideBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        int count = TuningOverrideStore.instance.EnabledCount;
        if (count > 0)
            Debug.LogWarning($"[Tuning] 켜진 튜닝 덮어쓰기 {count}개가 있습니다. 빌드에는 반영되지 않습니다. 확정한 값이면 엑셀(DataTables/GameData.xlsx)에 옮기세요.");
    }
}
