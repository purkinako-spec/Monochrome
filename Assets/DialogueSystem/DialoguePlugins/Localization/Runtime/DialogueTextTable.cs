using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Dialogue.Plugins.Localization
{
    [Serializable]
    public class DialogueTextEntry
    {
        [Tooltip("対応するノードID")]
        public string nodeID;

        [Tooltip("このノード独自の話者名（空欄の場合はスピーカーテーブルを参照）")]
        public string speakerName;

        [TextArea(2, 6), Tooltip("翻訳後のセリフ本文")]
        public string dialogueText;

        [Tooltip("このノードに対応する言語別ボイス（任意）")]
        public AudioClip voiceClip;
    }

    [Serializable]
    public class SpeakerTranslationEntry
    {
        [Tooltip("話者の内部識別ID（例: ellis, guide）")]
        public string speakerID;

        [Tooltip("この言語で画面に表示する名前（例: エリス, Ellis）")]
        public string displayName;
    }

    [Serializable]
    public class ChoiceTranslationEntry
    {
        [Tooltip("選択肢の識別キー（例: nodeID#c0, または targetNodeID）")]
        public string choiceKey;

        [Tooltip("翻訳後の選択肢テキスト")]
        public string choiceText;
    }

    /// <summary>
    /// 1つの言語に対応する会話テキスト・話者名・選択肢の辞書データ。
    /// （CSVからのインポートや、Inspectorでの直接入力に対応）
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueTextTable", menuName = "Dialogue/Localization/Text Table")]
    public class DialogueTextTable : ScriptableObject
    {
        [Tooltip("言語コード（例: ja, en, zh-CN, ko 等）")]
        [SerializeField] private string languageCode = "en";

        [Tooltip("言語の表示名（例: English, 日本語 等）")]
        [SerializeField] private string languageDisplayName = "English";

        [Header("Translations")]
        [SerializeField] private List<DialogueTextEntry> entries = new List<DialogueTextEntry>();
        [SerializeField] private List<SpeakerTranslationEntry> speakerEntries = new List<SpeakerTranslationEntry>();
        [SerializeField] private List<ChoiceTranslationEntry> choiceEntries = new List<ChoiceTranslationEntry>();

        public string LanguageCode => languageCode;
        public string LanguageDisplayName => languageDisplayName;
        public IReadOnlyList<DialogueTextEntry> Entries => entries;
        public IReadOnlyList<SpeakerTranslationEntry> SpeakerEntries => speakerEntries;
        public IReadOnlyList<ChoiceTranslationEntry> ChoiceEntries => choiceEntries;

        private Dictionary<string, DialogueTextEntry> textCache = new Dictionary<string, DialogueTextEntry>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> speakerCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> choiceCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool isCacheInitialized = false;

        private void OnEnable()
        {
            InvalidateCache();
        }

        public void InvalidateCache()
        {
            isCacheInitialized = false;
        }

        private void EnsureCache()
        {
            if (isCacheInitialized) return;

            textCache.Clear();
            foreach (var entry in entries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.nodeID))
                {
                    textCache[entry.nodeID] = entry;
                }
            }

            speakerCache.Clear();
            foreach (var sp in speakerEntries)
            {
                if (sp != null && !string.IsNullOrEmpty(sp.speakerID))
                {
                    speakerCache[sp.speakerID] = sp.displayName;
                }
            }

            choiceCache.Clear();
            foreach (var ch in choiceEntries)
            {
                if (ch != null && !string.IsNullOrEmpty(ch.choiceKey))
                {
                    choiceCache[ch.choiceKey] = ch.choiceText;
                }
            }

            isCacheInitialized = true;
        }

        public bool TryGetText(string nodeID, out string text)
        {
            EnsureCache();
            if (!string.IsNullOrEmpty(nodeID) && textCache.TryGetValue(nodeID, out var entry) && !string.IsNullOrEmpty(entry.dialogueText))
            {
                text = entry.dialogueText;
                return true;
            }
            text = null;
            return false;
        }

        public bool TryGetSpeaker(string speakerID, out string displayName)
        {
            EnsureCache();
            if (!string.IsNullOrEmpty(speakerID))
            {
                if (speakerCache.TryGetValue(speakerID, out var name) && !string.IsNullOrEmpty(name))
                {
                    displayName = name;
                    return true;
                }
            }
            displayName = null;
            return false;
        }

        public bool TryGetChoice(string choiceKey, out string choiceText)
        {
            EnsureCache();
            if (!string.IsNullOrEmpty(choiceKey) && choiceCache.TryGetValue(choiceKey, out var val) && !string.IsNullOrEmpty(val))
            {
                choiceText = val;
                return true;
            }
            choiceText = null;
            return false;
        }

        public bool TryGetVoice(string nodeID, out AudioClip voiceClip)
        {
            EnsureCache();
            if (!string.IsNullOrEmpty(nodeID) && textCache.TryGetValue(nodeID, out var entry) && entry.voiceClip != null)
            {
                voiceClip = entry.voiceClip;
                return true;
            }
            voiceClip = null;
            return false;
        }

        // --- エディタ更新・CSV取込用メソッド ---

        public void SetLanguageInfo(string code, string dispName)
        {
            languageCode = code;
            languageDisplayName = dispName;
        }

        public void SetEntry(string nodeID, string speaker, string text, AudioClip voice = null)
        {
            if (string.IsNullOrEmpty(nodeID)) return;
            var existing = entries.Find(e => e.nodeID.Equals(nodeID, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.speakerName = speaker;
                existing.dialogueText = text;
                if (voice != null) existing.voiceClip = voice;
            }
            else
            {
                entries.Add(new DialogueTextEntry
                {
                    nodeID = nodeID,
                    speakerName = speaker,
                    dialogueText = text,
                    voiceClip = voice
                });
            }
            InvalidateCache();
        }

        public void SetSpeaker(string speakerID, string displayName)
        {
            if (string.IsNullOrEmpty(speakerID)) return;
            var existing = speakerEntries.Find(s => s.speakerID.Equals(speakerID, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.displayName = displayName;
            }
            else
            {
                speakerEntries.Add(new SpeakerTranslationEntry
                {
                    speakerID = speakerID,
                    displayName = displayName
                });
            }
            InvalidateCache();
        }

        public void SetChoice(string choiceKey, string choiceText)
        {
            if (string.IsNullOrEmpty(choiceKey)) return;
            var existing = choiceEntries.Find(c => c.choiceKey.Equals(choiceKey, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.choiceText = choiceText;
            }
            else
            {
                choiceEntries.Add(new ChoiceTranslationEntry
                {
                    choiceKey = choiceKey,
                    choiceText = choiceText
                });
            }
            InvalidateCache();
        }

        public void ClearAll()
        {
            entries.Clear();
            speakerEntries.Clear();
            choiceEntries.Clear();
            InvalidateCache();
        }
    }
}
