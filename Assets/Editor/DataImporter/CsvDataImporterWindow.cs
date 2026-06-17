using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// 엑셀/구글시트 -> CSV -> JSON 변환 임포터.
// DataManager가 읽는 Resources/Data/*.json 포맷에 맞춰 직접 JSON을 생성한다.
// 로컬 CSV 파일 또는 구글 시트 공유 URL(웹에 게시 / 링크 공유) 둘 다 지원.
public class CsvDataImporterWindow : EditorWindow
{
    private enum TableKind { Monster, Stage }

    private static readonly (string name, string type)[] MonsterColumns =
    {
        ("id", "int"),
        ("monsterName", "string"),
        ("prefabName", "string"),
        ("maxHp", "int"),
        ("goldReward", "int"),
        ("attackDamage", "int"),
        ("attackInterval", "float"),
        ("attackRange", "float"),
    };

    private static readonly (string name, string type)[] StageColumns =
    {
        ("stageNumber", "int"),
        ("normalMonsterId", "int"),
        ("bossMonsterId", "int"),
        ("monstersPerEncounter", "int"),
        ("encountersToComplete", "int"),
        ("monsterSpacing", "float"),
        ("bgTexturePath", "string"),
        ("clearType", "enum:StageClearType"),
    };

    private TableKind selectedTable = TableKind.Monster;
    private string csvPath = "";
    private string sheetUrl = "";
    private string lastLog = "CSV 파일을 선택하거나 구글 시트 URL을 입력하세요.";
    private Vector2 scroll;

    [MenuItem("Tools/Data Pipeline/CSV Importer")]
    public static void Open()
    {
        var win = GetWindow<CsvDataImporterWindow>("CSV 데이터 임포터");
        win.minSize = new Vector2(460, 480);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        GUILayout.Label("엑셀/구글시트 → JSON 데이터 임포터", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Resources/Data 안의 JSON을 CSV(로컬 파일 또는 구글 시트 URL)로부터 갱신합니다.",
            MessageType.Info);

        EditorGUILayout.Space(6);
        selectedTable = (TableKind)EditorGUILayout.EnumPopup("대상 테이블", selectedTable);

        // ── 로컬 CSV 파일 ──
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("① 로컬 CSV 파일", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("선택된 파일", string.IsNullOrEmpty(csvPath) ? "(없음)" : csvPath);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("CSV 파일 선택..."))
            {
                string picked = EditorUtility.OpenFilePanel("CSV 선택", Application.dataPath, "csv");
                if (!string.IsNullOrEmpty(picked))
                {
                    csvPath = picked;
                }
            }

            if (GUILayout.Button("샘플 CSV 만들기"))
            {
                ExportSampleCsv(selectedTable);
            }
        }

        GUI.enabled = !string.IsNullOrEmpty(csvPath) && File.Exists(csvPath);
        if (GUILayout.Button("로컬 파일로 Import", GUILayout.Height(28)))
        {
            RunImport(() =>
            {
                string csvText = File.ReadAllText(csvPath);
                return ImportText(selectedTable, csvText, Path.GetFileName(csvPath));
            });
        }
        GUI.enabled = true;

        // ── 구글 시트 ──
        EditorGUILayout.Space(14);
        EditorGUILayout.LabelField("② 구글 시트 URL", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "시트 공유 설정이 '링크가 있는 모든 사용자(뷰어)' 또는 '웹에 게시'여야 외부에서 읽을 수 있습니다.",
            MessageType.None);
        sheetUrl = EditorGUILayout.TextField("시트 URL", sheetUrl);

        GUI.enabled = !string.IsNullOrEmpty(sheetUrl);
        if (GUILayout.Button("구글 시트에서 Import", GUILayout.Height(28)))
        {
            RunImport(() =>
            {
                string csvUrl = NormalizeGoogleSheetUrl(sheetUrl);
                string csvText = DownloadCsv(csvUrl);
                return ImportText(selectedTable, csvText, "Google Sheets");
            });
        }
        GUI.enabled = true;

        // ── 로그 ──
        EditorGUILayout.Space(14);
        EditorGUILayout.LabelField("결과 로그", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(lastLog, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void RunImport(Func<string> action)
    {
        try
        {
            lastLog = action();
        }
        catch (Exception e)
        {
            lastLog = "임포트 실패: " + e.Message;
            Debug.LogException(e);
        }
    }

    private static (string name, string type)[] GetColumns(TableKind kind)
    {
        return kind == TableKind.Monster ? MonsterColumns : StageColumns;
    }

    private static string GetTargetJsonPath(TableKind kind)
    {
        string fileName = kind == TableKind.Monster ? "MonsterDatas.json" : "StageDatas.json";
        return Path.Combine(Application.dataPath, "Resources", "Data", fileName);
    }

    private static void ExportSampleCsv(TableKind kind)
    {
        var columns = GetColumns(kind);
        string defaultName = kind == TableKind.Monster ? "MonsterDatas_template.csv" : "StageDatas_template.csv";
        string savePath = EditorUtility.SaveFilePanel("샘플 CSV 저장", Application.dataPath, defaultName, "csv");
        if (string.IsNullOrEmpty(savePath)) return;

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(c => c.name)));

        if (kind == TableKind.Monster)
        {
            sb.AppendLine("1001,BlueSlime,Slime_Blue,10,5,2,1.0,1.0");
            sb.AppendLine("2001,BossSlime,Slime_Boss,100,50,10,1.2,1.5");
        }
        else
        {
            sb.AppendLine("1,1001,2001,2,1,1.2,Textures/Backgrounds/Stage1,KillBoss");
        }

        File.WriteAllText(savePath, sb.ToString());
        EditorUtility.RevealInFinder(savePath);
    }

    // 구글시트 일반 공유 URL(.../edit#gid=123) -> CSV export URL로 변환.
    // 이미 export?format=csv 형태면 그대로 사용.
    private static string NormalizeGoogleSheetUrl(string rawUrl)
    {
        if (rawUrl.Contains("/export") && rawUrl.Contains("format=csv"))
        {
            return rawUrl;
        }

        var idMatch = Regex.Match(rawUrl, @"/d/([a-zA-Z0-9-_]+)");
        if (!idMatch.Success)
        {
            return rawUrl; // 패턴이 다르면 입력값 그대로 시도
        }

        string sheetId = idMatch.Groups[1].Value;

        var gidMatch = Regex.Match(rawUrl, @"gid=(\d+)");
        string gid = gidMatch.Success ? gidMatch.Groups[1].Value : "0";

        return $"https://docs.google.com/spreadsheets/d/{sheetId}/export?format=csv&gid={gid}";
    }

    private static string DownloadCsv(string url)
    {
        using (var client = new WebClient())
        {
            client.Encoding = Encoding.UTF8;
            string text = client.DownloadString(url);

            string trimmedStart = text.TrimStart();
            if (trimmedStart.StartsWith("<!DOCTYPE") || trimmedStart.StartsWith("<html"))
            {
                throw new Exception("CSV가 아니라 HTML 페이지가 반환되었습니다. 시트 공유 설정을 '링크가 있는 모든 사용자(뷰어)' 또는 '웹에 게시'로 바꿔주세요.");
            }

            return text;
        }
    }

    private static string ImportText(TableKind kind, string csvText, string sourceLabel)
    {
        var columns = GetColumns(kind);
        var rows = SimpleCsv.Parse(csvText);

        if (rows.Count < 2)
        {
            return "CSV에 헤더와 데이터가 충분하지 않습니다.";
        }

        var header = rows[0];
        var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Count; i++)
        {
            colIndex[header[i].Trim()] = i;
        }

        var missing = columns.Select(c => c.name).Where(n => !colIndex.ContainsKey(n)).ToList();
        if (missing.Count > 0)
        {
            return "CSV에 다음 컬럼이 없습니다: " + string.Join(", ", missing);
        }

        var array = new JArray();
        var log = new StringBuilder();
        var seenIds = new HashSet<int>();
        int successCount = 0;
        string idColumnName = columns[0].name;

        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0]))
            {
                continue;
            }

            var obj = new JObject();
            bool rowOk = true;

            foreach (var column in columns)
            {
                int idx = colIndex[column.name];
                string raw = idx < row.Count ? row[idx].Trim() : "";

                if (column.type == "int")
                {
                    if (!int.TryParse(raw, out int v))
                    {
                        log.AppendLine($"{r + 1}행: '{column.name}' 정수 변환 실패 (값: '{raw}')");
                        rowOk = false;
                        break;
                    }
                    obj[column.name] = v;
                }
                else if (column.type == "float")
                {
                    if (!float.TryParse(raw, out float v))
                    {
                        log.AppendLine($"{r + 1}행: '{column.name}' 실수 변환 실패 (값: '{raw}')");
                        rowOk = false;
                        break;
                    }
                    obj[column.name] = v;
                }
                else if (column.type.StartsWith("enum:"))
                {
                    string enumTypeName = column.type.Substring("enum:".Length);
                    Type enumType = ResolveType(enumTypeName);

                    bool parsed = false;
                    if (enumType != null)
                    {
                        try
                        {
                            object enumVal = Enum.Parse(enumType, raw, true);
                            obj[column.name] = (int)enumVal;
                            parsed = true;
                        }
                        catch
                        {
                            // 숫자 fallback
                        }
                    }

                    if (!parsed)
                    {
                        if (int.TryParse(raw, out int numericEnum))
                        {
                            obj[column.name] = numericEnum;
                        }
                        else
                        {
                            log.AppendLine($"{r + 1}행: '{column.name}' enum 변환 실패 (값: '{raw}')");
                            rowOk = false;
                            break;
                        }
                    }
                }
                else
                {
                    obj[column.name] = raw;
                }
            }

            if (!rowOk)
            {
                continue;
            }

            int idValue = obj[idColumnName]?.Value<int>() ?? -1;
            if (!seenIds.Add(idValue))
            {
                log.AppendLine($"{r + 1}행: ID {idValue} 중복 → 스킵");
                continue;
            }

            array.Add(obj);
            successCount++;
        }

        string targetPath = GetTargetJsonPath(kind);
        File.WriteAllText(targetPath, array.ToString(Formatting.Indented));
        AssetDatabase.Refresh();

        log.Insert(0, $"임포트 완료 ({sourceLabel}): {successCount}개 행 → {targetPath}\n\n");
        return log.ToString();
    }

    private static Type ResolveType(string typeName)
    {
        Type t = Type.GetType(typeName);
        if (t != null) return t;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType(typeName);
            if (t != null) return t;
        }
        return null;
    }
}
