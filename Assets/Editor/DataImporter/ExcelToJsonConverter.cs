using ExcelDataReader;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 엑셀 시트 → IData 목록 → JSON(Assets/Resources/Data/<시트 이름>.json) → SO 동기화(IDataSyncer).
// 원본 엑셀은 프로젝트 루트의 DataTables/GameData.xlsx다(시트 이름 = JSON 파일 이름, 첫 행 = 데이터 클래스의 필드 이름).
//
// 검사를 모두 통과해야만 JSON과 SO를 바꾼다. 값이 조용히 기본값(0, 빈 문자열)으로 바뀌는 길을 막기 위해서다.
//   - 엑셀 열이 데이터 클래스에 없다 / 같은 열 이름이 두 번 있다
//   - 데이터 클래스의 직렬화 필드에 해당하는 열이 엑셀에 없다(새 필드를 추가하고 엑셀에 열을 넣지 않은 경우)
//   - 값이 형식에 맞지 않는다(정수 칸의 소수, 없는 enum 값, 숫자가 아닌 글자)
//   - 같은 키(IData.Key)가 두 번 있다
// 빈 칸은 기본값으로 둔다(디자이너가 일부러 비운 칸).
public class ExcelToJsonConverter : EditorWindow
{
    // 프로젝트 루트 기준. Assets 밖에 두어 엑셀이 여는 임시 파일(~$...)을 Unity가 가져오지 않게 한다.
    public const string DefaultExcelPath = "DataTables/GameData.xlsx";

    private Vector2 scroll;
    private string lastLog = ""; // 로그 데이터

    //선택한 파일과 시트이름
    private string excelPath = "";
    private List<string> sheetNames = new List<string>();
    private int selectedSheetIndex = 0;

    //IData를 상속한 모든 클래스 타입 리스트
    private List<Type> targetClasses = new List<Type>();
    private string[] targetClassNames;
    private int selectedClassIndex = 0;
    //IDataSyncer를 구현한 모든 클래스의 인스턴스 (SO 동기화 자동 디스패치용)
    private List<IDataSyncer> syncers = new List<IDataSyncer>();

    [MenuItem("Tools/Excel to JSON Converter")]
    public static void ConvertExcelToJson()
    {
        var win = GetWindow<ExcelToJsonConverter>("Json 데이터 임포터");
        win.minSize = new Vector2(640, 520);
    }

    private void OnEnable()
    {
        FindAllDataClasses();
        syncers = FindAllSyncers();
        AddLog("데이터 임포터가 로드되었습니다. 데이터 클래스를 검색했습니다.");

        if (string.IsNullOrEmpty(excelPath) && File.Exists(DefaultExcelPath))
        {
            excelPath = Path.GetFullPath(DefaultExcelPath);
            LoadSheetNames(excelPath);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        GUILayout.Label("엑셀 → JSON 데이터 임포터", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("선택한 엑셀시트를 지정된 클래스 구조에 맞춰 Json으로 변환합니다.\n" +
                                $"원본: {DefaultExcelPath} (시트 이름 = JSON 파일 이름, 첫 행 = 필드 이름). 검사에 실패하면 아무것도 바꾸지 않습니다.", MessageType.Info);

        // 1. 타겟 클래스 선택
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("① 데이터 클래스 지정 (IData 상속 클래스 자동 검색)", EditorStyles.boldLabel);

        if (targetClasses.Count > 0)
        {
            int previous = selectedClassIndex;
            selectedClassIndex = EditorGUILayout.Popup("타겟 클래스", selectedClassIndex, targetClassNames);
            if (selectedClassIndex != previous) SelectSheetForClass();
        }
        else
        {
            EditorGUILayout.HelpBox("프로젝트에서 IData 인터페이스를 상속받는 클래스를 찾을 수 없습니다.", MessageType.Error);
            return;
        }

        // 2. 엑셀 파일 선택
        EditorGUILayout.Space(10);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("② 엑셀 파일 선택", EditorStyles.boldLabel);

            if (GUILayout.Button("파일 선택..."))
            {
                string folder = Path.GetDirectoryName(Path.GetFullPath(DefaultExcelPath));
                string startDir = Directory.Exists(folder) ? folder : Application.dataPath;
                string picked = EditorUtility.OpenFilePanel("Excel 선택", startDir, "xlsx,xls");
                if (!string.IsNullOrEmpty(picked))
                {
                    excelPath = picked;
                    LoadSheetNames(excelPath);
                }
            }
        }
        EditorGUILayout.LabelField("선택된 파일", string.IsNullOrEmpty(excelPath) ? "(없음)" : excelPath);

        // 3. 엑셀 파일의 시트 선택
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("③ 엑셀 시트 선택", EditorStyles.boldLabel);

        string selSheetName = "";
        if (sheetNames.Count > 0)
        {
            selectedSheetIndex = EditorGUILayout.Popup("시트 목록", selectedSheetIndex, sheetNames.ToArray());
            selSheetName = sheetNames[selectedSheetIndex];
        }

        // 4. 변환 버튼
        EditorGUILayout.Space(15);
        if (GUILayout.Button("Excel -> Json 변환 및 저장", GUILayout.Height(35)))
        {
            if (string.IsNullOrEmpty(excelPath) || string.IsNullOrEmpty(selSheetName))
            {
                EditorUtility.DisplayDialog("알림", "엑셀 파일과 시트를 올바르게 선택해주세요.", "확인");
                return;
            }

            Type selectedType = targetClasses[selectedClassIndex];

            // 변환 시작 전 로그 초기화
            lastLog = "";
            AddLog($"[{selSheetName}] 시트를 [{selectedType.Name}] 클래스로 변환 시작...");

            ConvertDynamic(excelPath, selSheetName, selectedType);
        }

        // 5. 실시간 로그 표시 영역
        EditorGUILayout.Space(14);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("결과 로그", EditorStyles.boldLabel);
            if (GUILayout.Button("로그 지우기", GUILayout.Width(80)))
            {
                lastLog = "";
            }
        }

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(lastLog, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void AddLog(string message)
    {
        string time = DateTime.Now.ToString("HH:mm:ss");
        lastLog += $"[{time}] {message}\n";

        // 스크롤을 항상 가장 아래로 내리기
        scroll.y = float.MaxValue;

        // 유니티 에디터 창의 UI를 즉시 강제 새로고침
        Repaint();
    }

    private void FindAllDataClasses()
    {
        targetClasses.Clear();
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IData).IsAssignableFrom(t))
            .ToList();

        targetClasses = types;
        targetClassNames = targetClasses.Select(t => t.Name).ToArray();
    }

    // IDataSyncer를 구현한 모든 클래스를 자동 탐색해서 인스턴스화.
    // 새 SO를 추가할 때 이 파일은 건드릴 필요 없이, IDataSyncer 구현 클래스만 추가하면 자동으로 여기서 잡힌다.
    public static List<IDataSyncer> FindAllSyncers()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IDataSyncer).IsAssignableFrom(t))
            .Select(t => (IDataSyncer)Activator.CreateInstance(t))
            .ToList();
    }

    private void LoadSheetNames(string path)
    {
        sheetNames.Clear();
        selectedSheetIndex = 0;

        try
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var dataSet = reader.AsDataSet();
                    foreach (DataTable table in dataSet.Tables)
                    {
                        sheetNames.Add(table.TableName);
                    }
                }
            }

            AddLog($"파일 로드 성공: 시트 {sheetNames.Count}개 발견.");
            SelectSheetForClass();
        }
        catch (Exception e)
        {
            AddLog($"❌ 시트 목록 로드 실패: {e.Message}");
        }
    }

    // 클래스에 맞는 시트(이름이 "클래스" 또는 "클래스 + base", 예: SkillData → SkillDatabase)를 고른다.
    private void SelectSheetForClass()
    {
        if (targetClassNames == null || targetClassNames.Length == 0 || sheetNames.Count == 0) return;

        string targetName = targetClassNames[selectedClassIndex];
        int matchIndex = sheetNames.FindIndex(s =>
            s.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
            s.Equals(targetName + "base", StringComparison.OrdinalIgnoreCase));
        if (matchIndex != -1) selectedSheetIndex = matchIndex;
    }

    private void ConvertDynamic(string filePath, string sheetName, Type targetType)
    {
        try
        {
            if (!TryReadSheet(filePath, sheetName, targetType, AddLog, out IList dataList))
            {
                AddLog("❌ 변환 취소: 위 문제를 고친 뒤 다시 변환하세요. JSON과 데이터베이스는 바뀌지 않았습니다.");
                EditorUtility.DisplayDialog("변환 취소", "엑셀 검사에 실패했습니다. 아래 로그창을 확인하세요.", "확인");
                return;
            }

            AddLog($"검사 통과: {dataList.Count}행. JSON 저장과 데이터베이스 동기화를 시작합니다...");
            SaveJsonAndSync(dataList, sheetName, targetType, syncers, AddLog);

            AddLog($"✅ 성공: 총 {dataList.Count}개의 데이터를 {sheetName}.json 으로 변환 완료!");
            EditorUtility.DisplayDialog("성공", $"{sheetName}.json 변환 및 저장 완료!", "확인");
        }
        catch (Exception e)
        {
            AddLog($"❌ 치명적 오류 발생: {e.Message}");
            EditorUtility.DisplayDialog("오류", "변환 중 오류가 발생했습니다. 로그 창을 확인하세요.", "확인");
        }
    }

    // ── 변환 핵심(대화상자 없음. 창과 검증 도구가 같이 쓴다) ──

    private class ColumnInfo
    {
        public string Header;
        public FieldInfo Field;
        public PropertyInfo Property;
        public Type TargetType;
    }

    // 엑셀 시트 하나를 targetType 목록으로 읽고 검사한다. 문제가 하나라도 있으면 false이고 dataList는 null이다.
    public static bool TryReadSheet(string filePath, string sheetName, Type targetType, Action<string> log, out IList dataList)
    {
        dataList = null;

        DataTable table;
        using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = ExcelReaderFactory.CreateReader(stream))
        {
            DataSet result = reader.AsDataSet();
            if (!result.Tables.Contains(sheetName))
            {
                log?.Invoke($"❌ '{sheetName}' 시트가 없습니다.");
                return false;
            }
            table = result.Tables[sheetName];
        }

        if (table.Rows.Count == 0)
        {
            log?.Invoke("❌ 시트가 비어 있습니다. 첫 행은 열 이름(필드 이름)이어야 합니다.");
            return false;
        }

        bool ok = true;

        // 1. 헤더: 엑셀 열 → 필드(없으면 프로퍼티)
        var columns = new Dictionary<int, ColumnInfo>();
        var seenHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mappedFields = new HashSet<FieldInfo>();
        for (int col = 0; col < table.Columns.Count; col++)
        {
            string header = table.Rows[0][col].ToString().Trim();
            if (string.IsNullOrEmpty(header)) continue;

            if (!seenHeaders.Add(header))
            {
                log?.Invoke($"❌ 같은 열 이름이 두 번 있습니다: '{header}'");
                ok = false;
                continue;
            }

            FieldInfo fieldInfo = GetFieldIgnoreCase(targetType, header);
            PropertyInfo propertyInfo = fieldInfo == null ? GetPropertyIgnoreCase(targetType, header) : null;

            if (fieldInfo != null)
            {
                columns[col] = new ColumnInfo { Header = header, Field = fieldInfo, TargetType = fieldInfo.FieldType };
                mappedFields.Add(fieldInfo);
            }
            else if (propertyInfo != null)
            {
                columns[col] = new ColumnInfo { Header = header, Property = propertyInfo, TargetType = propertyInfo.PropertyType };
            }
            else
            {
                log?.Invoke($"❌ 엑셀의 '{header}' 열이 {targetType.Name} 클래스에 없습니다.");
                ok = false;
            }
        }

        // 2. 데이터 클래스의 직렬화 필드마다 열이 있어야 한다. 없으면 그 값이 모든 행에서 기본값이 된다.
        foreach (FieldInfo field in SerializedFields(targetType))
        {
            if (mappedFields.Contains(field)) continue;
            log?.Invoke($"❌ {targetType.Name}.{field.Name} 열이 엑셀에 없습니다. 새로 추가한 필드라면 엑셀에 '{field.Name}' 열을 넣으세요.");
            ok = false;
        }

        if (!ok) return false;

        // 3. 행 → 인스턴스
        Type listType = typeof(List<>).MakeGenericType(targetType);
        IList list = (IList)Activator.CreateInstance(listType);
        var keys = new HashSet<int>();

        for (int i = 1; i < table.Rows.Count; i++)
        {
            DataRow row = table.Rows[i];
            if (row[0] == DBNull.Value || string.IsNullOrEmpty(row[0].ToString().Trim())) continue;

            object item = Activator.CreateInstance(targetType);
            foreach (var kvp in columns)
            {
                ColumnInfo column = kvp.Value;
                object raw = row[kvp.Key];
                if (raw == DBNull.Value || string.IsNullOrEmpty(raw.ToString().Trim())) continue; // 빈 칸 = 기본값

                object value = ConvertCell(raw, column.TargetType);
                if (value == null)
                {
                    log?.Invoke($"❌ {i + 1}행 '{column.Header}': {column.TargetType.Name} 형식에 맞지 않는 값 '{raw}'");
                    ok = false;
                    continue;
                }

                if (column.Field != null) column.Field.SetValue(item, value);
                else column.Property.SetValue(item, value);
            }

            int key = ((IData)item).Key;
            if (!keys.Add(key))
            {
                log?.Invoke($"❌ {i + 1}행: 키 {key}가 앞 행과 겹칩니다.");
                ok = false;
            }

            list.Add(item);
        }

        if (!ok) return false;

        dataList = list;
        return true;
    }

    // 읽은 목록을 JSON으로 저장하고, 그 타입을 맡은 Syncer로 SO 데이터베이스를 동기화한다.
    public static void SaveJsonAndSync(IList dataList, string sheetName, Type targetType, IReadOnlyList<IDataSyncer> syncers, Action<string> log)
    {
        JsonSerializerSettings settings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
        string jsonString = JsonConvert.SerializeObject(dataList, Formatting.Indented, settings);

        string savePath = Path.Combine(Application.dataPath, $"Resources/Data/{sheetName}.json");
        string directory = Path.GetDirectoryName(savePath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        File.WriteAllText(savePath, jsonString);
        AssetDatabase.Refresh();

        // 새 SO 추가 시 여기는 그대로 두고, IDataSyncer 구현 클래스(Syncer)만 추가하면 된다.
        IDataSyncer syncer = syncers.FirstOrDefault(s => s.DataType == targetType);
        if (syncer != null) syncer.Sync(dataList, log);
        else log?.Invoke($"[알림] {targetType.Name}을 맡은 Syncer가 없어 JSON만 저장했습니다.");
    }

    // Unity가 직렬화하는 인스턴스 필드(public 또는 [SerializeField], [NonSerialized] 제외).
    public static IEnumerable<FieldInfo> SerializedFields(Type type)
    {
        return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => !f.IsNotSerialized && (f.IsPublic || f.IsDefined(typeof(SerializeField), true)));
    }

    // 이름이 대소문자만 다른 멤버가 여러 개 있어도(예: MonsterData의 ID/id) 예외 없이 하나를 골라낸다.
    // 정확한 대소문자 일치를 우선하고, 없으면 대소문자 무시 매칭 중 첫 번째를 사용한다.
    private static FieldInfo GetFieldIgnoreCase(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return type.GetField(name, flags)
            ?? type.GetFields(flags).FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static PropertyInfo GetPropertyIgnoreCase(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var exact = type.GetProperty(name, flags);
        if (exact != null && exact.CanWrite) return exact;

        return type.GetProperties(flags)
            .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.CanWrite);
    }

    // 셀 값 하나를 필드 형식으로 바꾼다. 형식에 맞지 않으면 null(호출하는 쪽이 오류로 처리한다).
    // 숫자 셀은 double로 오므로 문자열을 거치지 않고 바꾼다(문화권과 반올림 영향 없음).
    public static object ConvertCell(object raw, Type targetType)
    {
        if (raw == null || raw == DBNull.Value) return null;

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            targetType = Nullable.GetUnderlyingType(targetType);

        string text = Convert.ToString(raw, CultureInfo.InvariantCulture).Trim();
        if (text.Length == 0) return null;

        if (targetType == typeof(string)) return text;

        if (targetType.IsEnum)
        {
            if (!Enum.TryParse(targetType, text, true, out object parsed)) return null;
            return Enum.IsDefined(targetType, parsed) ? parsed : null;
        }

        if (targetType == typeof(bool))
        {
            if (text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (text == "0" || text.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            return null;
        }

        double number;
        if (raw is double d) number = d;
        else if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return null;
        if (double.IsNaN(number) || double.IsInfinity(number)) return null;

        try
        {
            if (targetType == typeof(int) || targetType == typeof(long) || targetType == typeof(short))
            {
                if (number != Math.Floor(number)) return null; // 정수 칸의 소수는 반올림하지 않고 오류로
                return Convert.ChangeType(number, targetType, CultureInfo.InvariantCulture);
            }
            if (targetType == typeof(float))
            {
                float f = (float)number;
                return float.IsInfinity(f) ? null : (object)f;
            }
            if (targetType == typeof(double)) return number;

            return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
        }
        catch (OverflowException) { return null; }
        catch (InvalidCastException) { return null; }
        catch (FormatException) { return null; }
    }
}
