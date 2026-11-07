using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Runtime.Dialogue.Core
{
    [Serializable]
    public class FontStyleData
    {
        [Tooltip("フォントのスタイル名・識別子（例: default, horror, robot, pixel, cute 等）")]
        public string styleName;

        [Tooltip("このスタイルに割り当てるフォントアセット")]
        public TMP_FontAsset font;
    }

    /// <summary>
    /// SEデータベースやポートレートデータベースと同様に、
    /// 演出やキャラクターごとのフォントスタイルを1つにまとめて管理するデータベース。
    /// （言語ごとの差し替えやAddressablesによるロードにも対応）
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueFontDatabase", menuName = "Dialogue/Font Database")]
    public class DialogueFontDatabase : ScriptableObject
    {
        [Tooltip("フォントスタイルの一覧")]
        [SerializeField] private List<FontStyleData> fontStyles = new List<FontStyleData>();

        public IReadOnlyList<FontStyleData> FontStyles => fontStyles;

        /// <summary>
        /// 指定されたスタイル名のフォントを取得します。
        /// 見つからない場合は 'default'、それも無ければ登録されている最初のフォントを返します。
        /// </summary>
        public TMP_FontAsset GetFont(string styleName)
        {
            if (!string.IsNullOrEmpty(styleName))
            {
                var match = fontStyles.Find(f => f != null && f.styleName.Equals(styleName, StringComparison.OrdinalIgnoreCase));
                if (match != null && match.font != null)
                {
                    return match.font;
                }
            }

            return GetDefaultFont();
        }

        /// <summary>
        /// 標準（default）のフォントを取得します。
        /// </summary>
        public TMP_FontAsset GetDefaultFont()
        {
            var def = fontStyles.Find(f => f != null && f.styleName.Equals("default", StringComparison.OrdinalIgnoreCase));
            if (def != null && def.font != null) return def.font;

            // default がなければ最初に有効なフォントをフォールバックとして返す
            var first = fontStyles.Find(f => f != null && f.font != null);
            return first?.font;
        }

        /// <summary>
        /// スタイルとフォントのペアを動的に追加・更新します
        /// </summary>
        public void AddStyle(string styleName, TMP_FontAsset font)
        {
            if (string.IsNullOrEmpty(styleName) || font == null) return;
            var existing = fontStyles.Find(f => f != null && f.styleName.Equals(styleName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.font = font;
            }
            else
            {
                fontStyles.Add(new FontStyleData { styleName = styleName, font = font });
            }
        }
    }
}
