using System;
using System.Collections.Generic;
using UnityEngine;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Plugins.Localization
{
    /// <summary>
    /// 各言語に紐づくテキスト辞書およびフォントデータベースの設定データ。
    /// （直参照と Addressables アドレスの両方に対応）
    /// </summary>
    [Serializable]
    public class LanguageAssetConfig
    {
        [Tooltip("言語コード（例: ja, en, zh-CN, ko 等）")]
        public string languageCode = "ja";

        [Tooltip("言語のUI表示名（例: 日本語, English 等）")]
        public string displayName = "日本語";

        [Header("Direct References (直参照・即座に利用可能)")]
        [Tooltip("この言語の会話テキスト辞書アセット")]
        public DialogueTextTable textTable;

        [Tooltip("この言語のフォントデータベース（フォントスタイルの差し替え用）")]
        public DialogueFontDatabase fontDatabase;

        [Header("Addressables (任意・将来のアセットバンドル用)")]
        [Tooltip("Addressables を使用する場合の TextTable アドレス")]
        public string textTableAddress = "";

        [Tooltip("Addressables を使用する場合の FontDatabase アドレス")]
        public string fontDatabaseAddress = "";
    }

    /// <summary>
    /// 多言語システム全体でサポートする言語一覧と初期設定を保持するマスターアセット。
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueLocalizationConfig", menuName = "Dialogue/Localization/Localization Config")]
    public class DialogueLocalizationConfig : ScriptableObject
    {
        [Tooltip("ゲーム起動時のデフォルト言語コード")]
        [SerializeField] private string defaultLanguageCode = "ja";

        [Tooltip("サポートする言語の設定リスト")]
        [SerializeField] private List<LanguageAssetConfig> supportedLanguages = new List<LanguageAssetConfig>();

        public string DefaultLanguageCode => defaultLanguageCode;
        public IReadOnlyList<LanguageAssetConfig> SupportedLanguages => supportedLanguages;

        public LanguageAssetConfig GetConfig(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            return supportedLanguages.Find(c => c != null && c.languageCode.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        public bool SupportsLanguage(string code)
        {
            return GetConfig(code) != null;
        }
    }
}
