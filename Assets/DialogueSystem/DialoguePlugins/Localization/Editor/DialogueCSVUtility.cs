using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Runtime.Dialogue.Plugins.Localization.Editor
{
    /// <summary>
    /// RFC 4180 準拠の安全な CSV パーサー＆ライター。
    /// （Excel 文字化けを防ぐ UTF-8 BOM 出力、カンマ・改行・ダブルクォートのエスケープに完全対応）
    /// </summary>
    public static class DialogueCSVUtility
    {
        /// <summary>
        /// CSV テキストを行・列のリストにパースします
        /// </summary>
        public static List<List<string>> ParseCSV(string csvText)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(csvText)) return rows;

            var currentRow = new List<string>();
            var currentCell = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csvText.Length; i++)
            {
                char c = csvText[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // 連続する "" は 1 つの " として扱う
                        if (i + 1 < csvText.Length && csvText[i + 1] == '"')
                        {
                            currentCell.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentCell.Append(c);
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
                        currentRow.Add(currentCell.ToString());
                        currentCell.Clear();
                    }
                    else if (c == '\r')
                    {
                        if (i + 1 < csvText.Length && csvText[i + 1] == '\n')
                        {
                            i++;
                        }
                        currentRow.Add(currentCell.ToString());
                        currentCell.Clear();
                        rows.Add(currentRow);
                        currentRow = new List<string>();
                    }
                    else if (c == '\n')
                    {
                        currentRow.Add(currentCell.ToString());
                        currentCell.Clear();
                        rows.Add(currentRow);
                        currentRow = new List<string>();
                    }
                    else
                    {
                        currentCell.Append(c);
                    }
                }
            }

            if (currentCell.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(currentCell.ToString());
                rows.Add(currentRow);
            }

            return rows;
        }

        /// <summary>
        /// 行・列の二次元リストを RFC 4180 準拠の CSV 文字列に変換します
        /// </summary>
        public static string ToCSV(List<List<string>> rows)
        {
            var sb = new StringBuilder();
            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                for (int c = 0; c < row.Count; c++)
                {
                    if (c > 0) sb.Append(',');
                    sb.Append(EscapeCSVCell(row[c]));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// セル内のカンマ、ダブルクォート、改行を適切にクォート・エスケープします
        /// </summary>
        public static string EscapeCSVCell(string cell)
        {
            if (string.IsNullOrEmpty(cell)) return "";

            bool mustQuote = cell.Contains(",") || cell.Contains("\"") || cell.Contains("\n") || cell.Contains("\r");
            if (!mustQuote) return cell;

            return "\"" + cell.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>
        /// Excelで開いても文字化けしない UTF-8 BOM付きでファイルに書き込みます
        /// </summary>
        public static void WriteToFile(string filePath, string csvContent)
        {
            File.WriteAllText(filePath, csvContent, new UTF8Encoding(true));
        }

        /// <summary>
        /// ファイルから CSV テキストを読み込みます
        /// </summary>
        public static string ReadFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;
            return File.ReadAllText(filePath, Encoding.UTF8);
        }
    }
}
