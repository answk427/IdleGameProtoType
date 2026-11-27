using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// 튜닝 덮어쓰기(docs/COMBAT_ARCHITECTURE.md 8-7, D15): 엑셀을 고치지 않고 값을 바꿔 보는 에디터 전용 층.
//
// - 게임이 데이터베이스를 쓰기 전에 Prepare로 사본을 받는다. 에디터 플레이에서는 Instantiate한 사본에 덮어쓰기를 적용하고,
//   빌드에서는 원본을 그대로 돌려준다(이 파일의 나머지 코드는 빌드에 없다). 엑셀도 .asset도 바뀌지 않는다.
// - 덮어쓰기 목록은 에디터 쪽(TuningOverrideStore, UserSettings/ 폴더, 저장소에 올라가지 않음)이 Provider로 넣어 준다.
// - 플레이 중에 목록이 바뀌면 Reapply: 데이터 객체를 바꾸지 않고 값만 원본으로 되돌린 뒤 다시 덮어쓴다.
//   그래서 데이터를 참조하는 쪽(스킬, 스탯)이 그대로 새 값을 본다. 스폰할 때 읽는 값(몬스터)은 다음 스폰부터 반영된다.
public static class DataTuning
{
    public readonly struct Override
    {
        public readonly Type DataType;
        public readonly int Key;
        public readonly FieldInfo Field;
        public readonly object Value;

        public Override(Type dataType, int key, FieldInfo field, object value)
        {
            DataType = dataType;
            Key = key;
            Field = field;
            Value = value;
        }
    }

#if UNITY_EDITOR
    // 에디터 쪽이 연결한다. 검사를 통과한 덮어쓰기만 돌려준다.
    public static Func<IReadOnlyList<Override>> Provider;

    // 플레이 중 다시 적용한 뒤에 알린다(플레이어 능력치 다시 계산).
    public static event Action Changed;

    private static readonly List<(IDataTable source, IDataTable copy)> prepared = new List<(IDataTable, IDataTable)>();

    // 도메인 리로드를 끈 설정에서도 이전 플레이의 사본이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        prepared.Clear();
        Changed = null;
    }
#endif

    public static T Prepare<T>(T table) where T : ScriptableObject, IDataTable
    {
#if UNITY_EDITOR
        if (table == null || !Application.isPlaying) return table;

        T copy = UnityEngine.Object.Instantiate(table);
        copy.name = table.name + " (튜닝 사본)";
        prepared.Add((table, copy));
        ApplyTo(table, copy);
        return copy;
#else
        return table;
#endif
    }

#if UNITY_EDITOR
    public static void Reapply()
    {
        for (int i = 0; i < prepared.Count; i++) ApplyTo(prepared[i].source, prepared[i].copy);
        Changed?.Invoke();
    }

    private static void ApplyTo(IDataTable source, IDataTable copy)
    {
        // 1) 원본 값으로 되돌린다(지워진 덮어쓰기가 남지 않게)
        int rows = Math.Min(source.RowCount, copy.RowCount);
        for (int i = 0; i < rows; i++)
        {
            IData from = source.GetRow(i);
            IData to = copy.GetRow(i);
            if (from != null && to != null) CopySerializedFields(from, to);
        }

        // 2) 덮어쓴다
        IReadOnlyList<Override> overrides = Provider?.Invoke();
        if (overrides == null) return;

        for (int o = 0; o < overrides.Count; o++)
        {
            Override item = overrides[o];
            if (item.DataType != copy.DataType) continue;

            IData row = FindRow(copy, item.Key);
            if (row == null)
            {
                Debug.LogError($"[DataTuning] {item.DataType.Name}에 키 {item.Key}인 행이 없습니다. 덮어쓰지 않습니다.");
                continue;
            }

            object before = item.Field.GetValue(row);
            item.Field.SetValue(row, item.Value);
            if (row.Key != item.Key)
            {
                item.Field.SetValue(row, before);
                Debug.LogError($"[DataTuning] {item.DataType.Name}.{item.Field.Name}은 키 열이라 덮어쓸 수 없습니다.");
            }
        }
    }

    private static IData FindRow(IDataTable table, int key)
    {
        for (int i = 0; i < table.RowCount; i++)
        {
            IData row = table.GetRow(i);
            if (row != null && row.Key == key) return row;
        }
        return null;
    }

    private static void CopySerializedFields(object from, object to)
    {
        foreach (FieldInfo field in from.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.IsNotSerialized) continue;
            if (field.IsPublic || field.IsDefined(typeof(SerializeField), true)) field.SetValue(to, field.GetValue(from));
        }
    }
#endif
}
