using System.Collections.Generic;
using System.Text;

// 쉼표/줄바꿈이 포함된 필드("...")도 처리하는 간단한 CSV 파서.
// 외부 라이브러리 의존성 없이 엑셀에서 export한 CSV를 그대로 읽기 위한 용도.
public static class SimpleCsv
{
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrEmpty(text)) return rows;

        string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = normalized.Split('\n');

        foreach (var line in lines)
        {
            rows.Add(ParseLine(line));
        }

        while (rows.Count > 0 && rows[rows.Count - 1].Count == 1 &&
               string.IsNullOrWhiteSpace(rows[rows.Count - 1][0]))
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return rows;
    }

    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Length = 0;
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
