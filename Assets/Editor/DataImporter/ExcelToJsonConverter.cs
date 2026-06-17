using ExcelDataReader;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class ExcelToJsonConverter : EditorWindow
{
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

    [MenuItem("Tools/Excel to JSON Converter")]
    public static void ConvertExcelToJson()
    {
        var win = GetWindow<ExcelToJsonConverter>("Json 데이터 임포터");
        win.minSize = new Vector2(640, 520);
    }

    private void OnEnable()
    {
        FindAllDataClasses();
        AddLog("데이터 임포터가 로드되었습니다. 데이터 클래스를 검색했습니다.");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        GUILayout.Label("엑셀 → JSON 데이터 임포터", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("선택한 엑셀시트를 지정된 클래스 구조에 맞춰 Json으로 변환합니다.", MessageType.Info);

        // 1. 타겟 클래스 선택
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("① 데이터 클래스 지정 (IData 상속 클래스 자동 검색)", EditorStyles.boldLabel);

        if (targetClasses.Count > 0)
        {
            selectedClassIndex = EditorGUILayout.Popup("타겟 클래스", selectedClassIndex, targetClassNames);
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
                string picked = EditorUtility.OpenFilePanel("Excel 선택", Application.dataPath, "xlsx,xls");
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

            if (targetClassNames.Length > 0)
            {
                string targetName = targetClassNames[selectedClassIndex];
                int matchIndex = sheetNames.FindIndex(s => s.Equals(targetName, StringComparison.OrdinalIgnoreCase));
                if (matchIndex != -1)
                {
                    selectedSheetIndex = matchIndex;
                }
            }
        }
        catch (Exception e)
        {
            AddLog($"❌ 시트 목록 로드 실패: {e.Message}");
        }
    }

    private class ColumnInfo 
    { 
        public FieldInfo Field; 
        public PropertyInfo Property; 
        public Type TargetType; 
    }

    private void ConvertDynamic(string filePath, string sheetName, Type targetType)
    {
        try
        {
            Type listType = typeof(List<>).MakeGenericType(targetType);
            IList dataList = (IList)Activator.CreateInstance(listType);

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var result = reader.AsDataSet();
                    DataTable table = result.Tables.Contains(sheetName) ? result.Tables[sheetName] : result.Tables[0];

                    Dictionary<int, ColumnInfo> columnCache = new Dictionary<int, ColumnInfo>();
                    HashSet<string> seenHeaders = new HashSet<string>();

                    bool isNotMatchType = false;

                    // 1. 헤더 분석 및 검증
                    for (int col = 0; col < table.Columns.Count; col++)
                    {
                        string fieldName = table.Rows[0][col].ToString().Trim();
                        if (string.IsNullOrEmpty(fieldName)) continue;

                        if (seenHeaders.Contains(fieldName)) continue;
                        seenHeaders.Add(fieldName);

                        FieldInfo fieldInfo = targetType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        PropertyInfo propertyInfo = targetType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);

                        if (fieldInfo != null)
                        {
                            columnCache[col] = new ColumnInfo { Field = fieldInfo, TargetType = fieldInfo.FieldType };
                        }
                        else if (propertyInfo != null && propertyInfo.CanWrite)
                        {
                            columnCache[col] = new ColumnInfo { Property = propertyInfo, TargetType = propertyInfo.PropertyType };
                        }
                        else
                        {
                            AddLog($"[검증 경고] 엑셀의 '{fieldName}' 컬럼이 {targetType.Name} 클래스에 없습니다.");
                            isNotMatchType = true;
                        }
                    }

                    if (isNotMatchType)
                    {
                        AddLog("❌ 변환 취소: 클래스 구조와 맞지 않는 컬럼이 발견되어 변환을 중단합니다.");
                        EditorUtility.DisplayDialog("변환 취소", "컬럼 불일치가 발견되었습니다. 아래 로그창을 확인하세요.", "확인");
                        return;
                    }

                    AddLog("컬럼 무결성 검증 통과. 데이터 인스턴스화 시작...");

                    // 2. 실제 데이터 인스턴스화
                    for (int i = 1; i < table.Rows.Count; i++)
                    {
                        var row = table.Rows[i];
                        if (row[0] == DBNull.Value || string.IsNullOrEmpty(row[0].ToString())) continue;

                        object item = Activator.CreateInstance(targetType);

                        foreach (var kvp in columnCache)
                        {
                            int col = kvp.Key;
                            ColumnInfo colInfo = kvp.Value;
                            object rawValue = row[col];

                            if (rawValue == DBNull.Value) continue;

                            object convertedValue = SafeChangeType(rawValue, colInfo.TargetType);

                            if (convertedValue != null)
                            {
                                if (colInfo.Field != null) colInfo.Field.SetValue(item, convertedValue);
                                else if (colInfo.Property != null) colInfo.Property.SetValue(item, convertedValue);
                            }
                            else
                            {
                                // 타입 변환 실패 시 로그 기록
                                AddLog($"[타입 오류] {colInfo.TargetType.Name} 형식에 맞지 않는 값 발견: '{rawValue}'");
                            }
                        }

                        dataList.Add(item);
                    }

                    // 3. JSON 직렬화 및 저장
                    JsonSerializerSettings settings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
                    string jsonString = JsonConvert.SerializeObject(dataList, Formatting.Indented, settings);

                    string savePath = Path.Combine(Application.dataPath, $"Resources/Data/{sheetName}.json");
                    string directory = Path.GetDirectoryName(savePath);
                    if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                    File.WriteAllText(savePath, jsonString);
                    AssetDatabase.Refresh();

                    AddLog($"✅ 성공: 총 {dataList.Count}개의 데이터를 {sheetName}.json 으로 변환 완료!");
                    EditorUtility.DisplayDialog("성공", $"{sheetName}.json 변환 및 저장 완료!", "확인");
                }
            }
        }
        catch (Exception e)
        {
            AddLog($"❌ 치명적 오류 발생: {e.Message}");
            EditorUtility.DisplayDialog("오류", "변환 중 오류가 발생했습니다. 로그 창을 확인하세요.", "확인");
        }
    }

    private static object SafeChangeType(object value, Type targetType)
    {
        if (value == null || value == DBNull.Value) return null;
        string stringValue = value.ToString().Trim();
        if (string.IsNullOrEmpty(stringValue)) return null;

        try
        {
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                targetType = Nullable.GetUnderlyingType(targetType);

            if (targetType == typeof(int) || targetType == typeof(long) || targetType == typeof(short))
                if (double.TryParse(stringValue, out double doubleVal)) return System.Convert.ChangeType(Math.Round(doubleVal), targetType);

            if (targetType == typeof(bool))
            {
                if (stringValue == "1" || stringValue.ToLower() == "true") return true;
                if (stringValue == "0" || stringValue.ToLower() == "false") return false;
            }

            if (targetType.IsEnum) return Enum.Parse(targetType, stringValue, true);

            return System.Convert.ChangeType(value, targetType);
        }
        catch { return null; }
    }
}