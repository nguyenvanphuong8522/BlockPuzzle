using System.Collections.Generic;
using System.Text;

namespace Survivor
{
    // Minimal RFC-4180 CSV reader: commas, "quoted fields", "" inside quotes, line breaks inside quotes, CRLF / LF.
    public static class CsvReader
    {
        public static List<List<string>> Parse(string text)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(text)) return rows;
            if (text[0] == '﻿') text = text.Substring(1); // UTF-8 BOM

            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else quoted = false;
                    }
                    else cell.Append(c);
                    continue;
                }
                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        row.Add(cell.ToString());
                        cell.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(cell.ToString());
                        cell.Clear();
                        rows.Add(row);
                        row = new List<string>();
                        break;
                    default:
                        cell.Append(c);
                        break;
                }
            }
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }
            return rows;
        }

        // One cell, quoted when needed (for writing the table from editor tools).
        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var needs = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || value.Trim() != value;
            return needs ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }
    }
}
