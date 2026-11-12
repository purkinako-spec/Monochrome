using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;
using Runtime.Dialogue.Audio;

namespace Runtime.Dialogue
{
    public class DialogueViewWindow : MonoBehaviour, IDialogueView
    {
        [Header("UI References")]
        [SerializeField] public GameObject windowRoot;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private Transform choiceButtonParent;
        [SerializeField] private Button choiceButtonPrefab;

        [Header("Audio Component")]
        [SerializeField] private DialogueTypewriterAudio typewriterAudio;

        [Header("Settings")]
        [SerializeField] private float typingSpeed = 0.05f;

        [Header("Fonts")]
        [SerializeField] private TMP_FontAsset defaultFont;
        [SerializeField, Tooltip("フォントスタイルを管理するデータベース（未設定時は defaultFont が使用されます）")]
        private DialogueFontDatabase fontDatabase;

        private bool isFontPersistent = false;
        private string currentSpeakerID = "";

        private Coroutine typingCoroutine;
        private string currentFullText;
        private List<DialogueCommand> currentCommands;
        private Action onCompleteCallback;
        private List<Button> activeButtons = new List<Button>();

        public bool IsTyping => typingCoroutine != null;
        public bool IsShowingChoices { get; private set; } = false;

        private void Awake()
        {
            if (typewriterAudio == null)
            {
                typewriterAudio = UnityEngine.Object.FindAnyObjectByType<DialogueTypewriterAudio>();
            }

            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.RegisterView(this);
            }
            CloseView();
        }

        private void Start()
        {
            // Awakeの実行順序によってDialogueManager.Instanceが未初期化だった場合のフォールバック登録
            if (DialogueManager.Instance != null && DialogueManager.Instance.CurrentView == null)
            {
                DialogueManager.Instance.RegisterView(this);
            }
        }

        /// <summary>
        /// フォントスタイル名（default, horror, robot等）からフォントを適用します。
        /// </summary>
        /// <summary>
        /// 使用するフォントデータベースを動的に変更します（多言語対応・言語切り替え用）。
        /// </summary>
        public void SetFontDatabase(DialogueFontDatabase db)
        {
            fontDatabase = db;
            ResetFont();
        }

        public DialogueFontDatabase FontDatabase => fontDatabase;

        public void SetFontStyle(string styleName, bool persistent = false)
        {
            TMP_FontAsset targetFont = null;
            if (fontDatabase != null)
            {
                targetFont = fontDatabase.GetFont(styleName);
            }
            if (targetFont == null)
            {
                targetFont = defaultFont;
            }
            SetFont(targetFont, persistent);
        }

        /// <summary>
        /// フォントアセットを直接適用します。
        /// </summary>
        public void SetFont(TMP_FontAsset font, bool persistent = false)
        {
            if (bodyText != null && font != null)
            {
                bodyText.font = font;
            }
            isFontPersistent = persistent;
        }

        /// <summary>
        /// 現在の話者またはデフォルトフォントに復帰（リセット）します。
        /// </summary>
        public void ResetFont()
        {
            isFontPersistent = false;
            ApplySpeakerFont(currentSpeakerID);
        }

        /// <summary>
        /// 現在の話者の CharacterProfile に基づいてフォントを適用します。
        /// 永続フラグが立っていない限り、各セリフの開始時に自動呼び出しされます。
        /// </summary>
        public void ApplySpeakerFont(string speakerID)
        {
            currentSpeakerID = speakerID;
            if (isFontPersistent) return;

            string style = "default";
            var portraitHandler = UnityEngine.Object.FindAnyObjectByType<Runtime.Dialogue.Plugins.Portraits.StandardUIPortraitHandler>();
            if (portraitHandler != null && !string.IsNullOrEmpty(speakerID))
            {
                var profile = portraitHandler.FindProfile(speakerID);
                if (profile != null && !string.IsNullOrEmpty(profile.defaultFontStyle))
                {
                    style = profile.defaultFontStyle;
                }
            }

            SetFontStyle(style, false);
        }

        public void InitializeView()
        {
            if (windowRoot != null) windowRoot.SetActive(true);
            if (bodyText != null) bodyText.enabled = true;
            if (nameText != null) nameText.enabled = true;

            if (bodyText != null) bodyText.text = string.Empty;
            if (nameText != null) nameText.text = string.Empty;

            var bgImage = windowRoot != null ? windowRoot.GetComponent<UnityEngine.UI.Image>() : null;
            if (bgImage != null) bgImage.enabled = true;
        }

        public void CloseView()
        {
            isFontPersistent = false;
            if (windowRoot != null) windowRoot.SetActive(false);
            if (bodyText != null) bodyText.enabled = false;
            if (nameText != null) nameText.enabled = false;

            HideChoices();
        }

        public void DisplaySentence(string speakerID, string cleanText, List<DialogueCommand> commands, Action onTypingComplete)
        {
            InitializeView();
            ApplySpeakerFont(speakerID);

            if (nameText != null) nameText.text = speakerID;
            currentFullText = cleanText;
            currentCommands = commands;
            onCompleteCallback = onTypingComplete;

            // 💡 バックログ機能の連携: 会話テキストが表示されるタイミングでログに追加
            if (DialogueLogManager.Instance != null && !string.IsNullOrWhiteSpace(cleanText))
            {
                DialogueLogManager.Instance.AddLog(speakerID, cleanText);
            }

            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypeTextRoutine());
        }

        public void DisplaySentence(string speakerID, string cleanText, Action onTypingComplete)
        {
            DisplaySentence(speakerID, cleanText, new List<DialogueCommand>(), onTypingComplete);
        }

        private IEnumerator TypeTextRoutine()
        {
            bodyText.text = currentFullText;
            bodyText.maxVisibleCharacters = 0;
            bodyText.ForceMeshUpdate();

            typewriterAudio?.ResetCounter();

            if (string.IsNullOrWhiteSpace(currentFullText))
            {
                ExecuteRemainingCommands(false);
                typingCoroutine = null;
                onCompleteCallback?.Invoke();
                yield break;
            }

            int totalVisibleChars = bodyText.textInfo.characterCount;
            int currentVisibleIndex = 0;

            while (currentVisibleIndex < totalVisibleChars)
            {
                int pendingCommands = 0;
                if (currentCommands != null)
                {
                    foreach (var cmd in currentCommands)
                    {
                        if (cmd != null && !cmd.IsExecuted && cmd.CharacterIndex == currentVisibleIndex)
                        {
                            pendingCommands++;
                            if (DialogueEventDispatcher.Instance != null)
                                DialogueEventDispatcher.Instance.ExecuteCommand(cmd, () => pendingCommands--);
                            else
                                pendingCommands--;
                        }
                    }
                }

                if (pendingCommands > 0) yield return new WaitUntil(() => pendingCommands <= 0);

                currentVisibleIndex++;
                bodyText.maxVisibleCharacters = currentVisibleIndex;

                typewriterAudio?.OnCharacterTyped();

                yield return new WaitForSeconds(typingSpeed);
            }

            ExecuteRemainingCommands(false);
            typingCoroutine = null;
            onCompleteCallback?.Invoke();
        }

        public void ForceCompleteTyping()
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }

            if (bodyText != null) bodyText.maxVisibleCharacters = 99999;

            ExecuteRemainingCommands(true);
            onCompleteCallback?.Invoke();
        }

        private void ExecuteRemainingCommands(bool forceComplete)
        {
            if (currentCommands == null) return;

            foreach (var cmd in currentCommands)
            {
                if (cmd != null && !cmd.IsExecuted)
                {
                    if (DialogueEventDispatcher.Instance != null)
                    {
                        if (forceComplete)
                            DialogueEventDispatcher.Instance.ForceCompleteCommand(cmd);
                        else
                            DialogueEventDispatcher.Instance.ExecuteCommand(cmd, null);
                    }
                }
            }
        }

        public void ShowChoices(List<ChoiceData> choices, Action<int> onChoiceSelected)
        {
            HideChoices();
            IsShowingChoices = true;

            for (int i = 0; i < choices.Count; ++i)
            {
                int index = i;
                Button btn = Instantiate(choiceButtonPrefab, choiceButtonParent);

                var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = choices[i].choiceText;

                btn.onClick.AddListener(() =>
                {
                    DialogueAudioManager.Instance?.PlaySE("choice_select");
                    onChoiceSelected?.Invoke(index);
                });
                activeButtons.Add(btn);
            }
        }

        public void HideChoices()
        {
            IsShowingChoices = false;
            foreach (var btn in activeButtons)
            {
                if (btn != null) Destroy(btn.gameObject);
            }
            activeButtons.Clear();
        }

        public void SetTypingSpeed(float newSpeed)
        {
            typingSpeed = Mathf.Max(0.001f, newSpeed);
        }
    }
}
