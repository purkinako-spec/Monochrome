using UnityEngine;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Plugins.Localization
{
    /// <summary>
    /// 現在ロードされている言語の DialogueTextTable を参照して、
    /// 本文・話者名・選択肢・ボイスのローカライズ解決を行うリゾルバー。
    /// （辞書に登録がない場合はデフォルト文字列をそのまま返す安全なフォールバック機構付き）
    /// </summary>
    public class LocalizationTextResolver : IDialogueTextResolver
    {
        private DialogueTextTable currentTable;

        public DialogueTextTable CurrentTable => currentTable;

        public LocalizationTextResolver(DialogueTextTable table = null)
        {
            currentTable = table;
        }

        public void SetTable(DialogueTextTable table)
        {
            currentTable = table;
        }

        public string ResolveText(string nodeID, string defaultText)
        {
            if (currentTable == null)
            {
                Debug.LogWarning($"[LocalizationTextResolver] currentTable is NULL! nodeID={nodeID}");
                return defaultText;
            }

            if (currentTable.TryGetText(nodeID, out var translated))
            {
                Debug.Log($"[LocalizationTextResolver] 翻訳成功: nodeID={nodeID} -> '{translated}'");
                return translated;
            }

            Debug.LogWarning($"[LocalizationTextResolver] 翻訳未検出: nodeID='{nodeID}' (Table: {currentTable.LanguageCode}, Entries: {currentTable.Entries.Count})");
            return defaultText;
        }

        public string ResolveSpeaker(string speakerID)
        {
            if (currentTable != null && currentTable.TryGetSpeaker(speakerID, out var displayName))
            {
                return displayName;
            }
            return speakerID;
        }

        public string ResolveChoice(string choiceKey, string defaultChoiceText)
        {
            if (currentTable != null && currentTable.TryGetChoice(choiceKey, out var choice))
            {
                return choice;
            }
            return defaultChoiceText;
        }

        public AudioClip ResolveVoice(string nodeID, AudioClip defaultVoice = null)
        {
            if (currentTable != null && currentTable.TryGetVoice(nodeID, out var voice))
            {
                return voice;
            }
            return defaultVoice;
        }
    }
}
