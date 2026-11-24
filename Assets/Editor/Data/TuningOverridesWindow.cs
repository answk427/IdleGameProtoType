using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 튜닝 덮어쓰기 창(docs/COMBAT_ARCHITECTURE.md 8-7, D15). IdleGame/Data/Tuning Overrides.
// 엑셀을 고치지 않고 값을 바꿔 본다. 에디터 플레이에서만 적용되고, 플레이 중에 바꾸면 바로 다시 적용된다.
// 정한 값은 "엑셀로 옮길 목록 복사"로 꺼내 엑셀에 적고 변환한 뒤, 여기서 지운다.
public class TuningOverridesWindow : EditorWindow
{
    private Vector2 scroll;
    private GUIStyle errorStyle;
    private GUIStyle baseValueStyle;

    [MenuItem("IdleGame/Data/Tuning Overrides")]
    public static void ShowWindow()
    {
        var window = GetWindow<TuningOverridesWindow>("튜닝 덮어쓰기");
        window.minSize = new Vector2(760, 260);
    }

    private void OnGUI()
    {
        EnsureStyles();
        TuningOverrideStore store = TuningOverrideStore.instance;

        EditorGUILayout.HelpBox(
            "엑셀을 고치지 않고 값을 바꿔 보는 곳입니다. 에디터 플레이에서만 적용되고 엑셀과 .asset은 바뀌지 않습니다.\n" +
            "내 PC에만 저장됩니다(UserSettings/). 플레이 중에 바꾸면 바로 다시 적용됩니다: 플레이어는 즉시, 몬스터는 다음 스폰부터.\n" +
            "정한 값은 엑셀(DataTables/GameData.xlsx)에 옮기고 변환한 뒤 여기서 지우세요.",
            MessageType.Info);

        if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("플레이 중: 바꾸면 바로 적용됩니다.", MessageType.None);

        bool changed = false;
        int removeAt = -1;

        scroll = EditorGUILayout.BeginScrollView(scroll);
        List<TuningOverrideStore.Entry> entries = store.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (DrawEntry(entries[i], out bool remove)) changed = true;
            if (remove) removeAt = i;
        }
        EditorGUILayout.EndScrollView();

        if (removeAt >= 0)
        {
            entries.RemoveAt(removeAt);
            changed = true;
        }

        EditorGUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ 추가", GUILayout.Width(80)))
            {
                entries.Add(new TuningOverrideStore.Entry { table = TuningOverrideStore.DataTypes.FirstOrDefault()?.Name ?? "" });
                changed = true;
            }

            EditorGUI.BeginDisabledGroup(store.EnabledCount == 0);
            if (GUILayout.Button("모두 끄기", GUILayout.Width(90)))
            {
                foreach (var e in entries) e.enabled = false;
                changed = true;
            }
            if (GUILayout.Button("엑셀로 옮길 목록 복사", GUILayout.Width(150)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildExcelList(entries);
                ShowNotification(new GUIContent("복사했습니다"));
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            GUILayout.Label($"켜짐 {store.EnabledCount} / {entries.Count}", EditorStyles.miniLabel);
        }

        if (changed) Commit(store);
    }

    // 한 줄: [켜기] 표 / 키 / 열 / 값  (엑셀 값)  [✕]  + 검사 결과
    private bool DrawEntry(TuningOverrideStore.Entry entry, out bool remove)
    {
        remove = false;
        EditorGUI.BeginChangeCheck();

        using (new EditorGUILayout.VerticalScope("box"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                entry.enabled = EditorGUILayout.Toggle(entry.enabled, GUILayout.Width(18));

                IReadOnlyList<Type> types = TuningOverrideStore.DataTypes;
                string[] typeNames = types.Select(t => t.Name).ToArray();
                int typeIndex = Mathf.Max(0, Array.IndexOf(typeNames, entry.table));
                typeIndex = EditorGUILayout.Popup(typeIndex, typeNames, GUILayout.Width(140));
                Type type = typeIndex < types.Count ? types[typeIndex] : null;
                entry.table = type?.Name ?? "";

                GUILayout.Label("키", GUILayout.Width(16));
                entry.key = EditorGUILayout.IntField(entry.key, GUILayout.Width(60));

                string[] fieldNames = type != null ? ExcelToJsonConverter.SerializedFields(type).Select(f => f.Name).ToArray() : new string[0];
                int fieldIndex = Mathf.Max(0, Array.IndexOf(fieldNames, entry.column));
                fieldIndex = fieldNames.Length > 0 ? EditorGUILayout.Popup(fieldIndex, fieldNames, GUILayout.Width(150)) : 0;
                entry.column = fieldNames.Length > 0 ? fieldNames[fieldIndex] : "";

                GUILayout.Label("=", GUILayout.Width(12));
                entry.value = EditorGUILayout.TextField(entry.value, GUILayout.MinWidth(80));

                GUILayout.Label($"(엑셀 값 {BaseValue(type, entry)})", baseValueStyle, GUILayout.Width(130));

                if (GUILayout.Button("✕", GUILayout.Width(24))) remove = true;
            }

            if (entry.enabled && !TuningOverrideStore.TryResolve(entry, out _, out string error))
                EditorGUILayout.LabelField(error, errorStyle);
        }

        return EditorGUI.EndChangeCheck();
    }

    private static string BaseValue(Type type, TuningOverrideStore.Entry entry)
    {
        FieldInfo field = TuningOverrideStore.FindField(type, entry.column);
        IData row = TuningOverrideStore.FindRow(TuningOverrideStore.FindTableAsset(type) as IDataTable, entry.key);
        return field != null && row != null ? Convert.ToString(field.GetValue(row), System.Globalization.CultureInfo.InvariantCulture) : "-";
    }

    // 시트 이름(데이터베이스 에셋 이름), 키, 열, 값. 탭으로 나눠 엑셀이나 메모에 붙여 넣기 쉽게.
    private static string BuildExcelList(List<TuningOverrideStore.Entry> entries)
    {
        var sb = new StringBuilder("시트\t키\t열\t값\n");
        foreach (var entry in entries.Where(e => e.enabled))
        {
            ScriptableObject asset = TuningOverrideStore.FindTableAsset(TuningOverrideStore.FindType(entry.table));
            sb.Append(asset != null ? asset.name : entry.table).Append('\t')
              .Append(entry.key).Append('\t')
              .Append(entry.column).Append('\t')
              .Append(entry.value).Append('\n');
        }
        return sb.ToString();
    }

    private static void Commit(TuningOverrideStore store)
    {
        store.SaveNow();
        if (EditorApplication.isPlaying) DataTuning.Reapply();
    }

    private void EnsureStyles()
    {
        if (errorStyle != null) return;
        errorStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.9f, 0.35f, 0.3f) } };
        baseValueStyle = new GUIStyle(EditorStyles.miniLabel);
    }
}
