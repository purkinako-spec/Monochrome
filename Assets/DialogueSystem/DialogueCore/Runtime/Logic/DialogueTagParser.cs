using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Runtime.Dialogue
{
    /// <summary>
    /// 生テキストから[コマンド:キー=値]形式のタグを解析して分離する文字列パースクラス
    /// </summary>
    public static class DialogueTagParser
    {
        // 独自タグ[コマンド名:パラメータ]を検出するための正規表現パターン
        private static readonly Regex TagRegex = new Regex(@"\[([^\]\s:]+)(?::([^\]]+))?\]");

        /// <summary>
        /// 生テキストを解析し、純粋な表示用文字列とコマンドのリストに分離して返す
        /// </summary>
        /// <param name="rawText">解析前のテキスト(例:"これ[se:clip=ping]やで")</param>
        /// <param name="cleanText">出力:タグがきれいに消去された表示用テキスト</param>
        /// <param name="commands">出力:何文字目で発火するかが記録されたコマンドのリスト</param>
        public static void ParseText(string rawText, out string cleanText, out List<DialogueCommand> commands)
        {
            commands = new List<DialogueCommand>();
            cleanText = string.Empty;
            if (string.IsNullOrEmpty(rawText)) return;

            // [コマンド] または <TMPタグ> を両方検知
            Regex combinedRegex = new Regex(@"\[([^\]\s:<]+)(?::([^\]<]+))?\]|<[^>]+>");

            int lastIndex = 0;
            int visibleCharCount = 0;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (Match match in combinedRegex.Matches(rawText))
            {
                string textBefore = rawText.Substring(lastIndex, match.Index - lastIndex);
                sb.Append(textBefore);

                // 絵文字(サロゲートペア)のズレを防ぐため StringInfo を使用
                visibleCharCount += new System.Globalization.StringInfo(textBefore).LengthInTextElements;

                if (match.Value.StartsWith("<"))
                {
                    // TMPタグは表示用テキストに残す（カウントは増やさない）
                    sb.Append(match.Value);
                }
                else
                {
                    // 独自コマンドの場合はリストに追加し、テキストからは消去
                    string cmdName = match.Groups[1].Value;
                    string paramStr = match.Groups[2].Value;
                    commands.Add(new DialogueCommand(cmdName, ParseArguments(paramStr), visibleCharCount));
                }
                lastIndex = match.Index + match.Length;
            }
            sb.Append(rawText.Substring(lastIndex));
            cleanText = sb.ToString();
        }

        /// <summary>
        /// "time=1.5,clip=bomb" のようなカンマ・イコール区切りの文字列を辞書型に分解する
        /// </summary>
        private static Dictionary<string, string> ParseArguments(string paramStr)
        {
            var dict = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(paramStr)) return dict;

            string[] pairs = paramStr.Split(',');
            foreach (string pair in pairs)
            {
                string[] kv = pair.Split('=');
                if (kv.Length == 2)
                {
                    string key = kv[0].Trim().ToLower();
                    string value = kv[1].Trim();
                    if (!dict.ContainsKey(key))
                    {
                        dict.Add(key, value);
                    }
                }
                else if (kv.Length == 1)
                {
                    // パラメータが値だけの場合(例:[se:bomb])は、キーを"default"として収納
                    string value = kv[0].Trim();
                    if (!dict.ContainsKey("default"))
                    {
                        dict.Add("default", value);
                    }
                }
            }
            return dict;
        }
    }
}