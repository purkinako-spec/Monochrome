using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Runtime.Dialogue.Logic;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Branching;

namespace Runtime.Dialogue.View
{
    /// <summary>
    /// 会話表示用 View 実装
    /// DialogueManager から DisplaySentence / ForceCompleteTyping / CloseView / InitializeView が呼ばれる
    /// </summary>
    public class DialogueView : MonoBehaviour, IDialogueView
    {
        [Header("UI References")]
        [SerializeField] private GameObject _rootObject;
        [SerializeField] private TMP_Text _dialogueText;
        [SerializeField] private TMP_Text _speakerNameText;
        [SerializeField] private GameObject _choicesContainer;
        [SerializeField] private GameObject _choiceButtonPrefab;
        [SerializeField] private GameObject _continuePrompt;

        [Header("Typewriter Settings")]
        [SerializeField] private float _typewriterSpeed = 0.02f;

        private Action _onCompleteCallback;
        private Coroutine _typewriterCoroutine;
        private bool _isTyping;
        private string _fullText;
        private List<DialogueCommand> _pendingCommands;

        private void Awake()
        {
            // ★重要: 自身を DialogueManager に登録
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.RegisterView(this);
            }
            else
            {
                Debug.LogError("[DialogueView] DialogueManager.Instance が見つかりません。シーンに DialogueManager が存在するか確認してください。");
            }

            InitializeView();
        }

        private void OnDestroy()
        {
            if (DialogueManager.Instance != null && DialogueManager.Instance.CurrentView == this)
            {
                DialogueManager.Instance.RegisterView(null);
            }
        }

        // ---- IDialogueView 実装 ----

        public void InitializeView()
        {
            if (_rootObject != null) _rootObject.SetActive(false);
            if (_continuePrompt != null) _continuePrompt.SetActive(false);
            if (_choicesContainer != null) _choicesContainer.SetActive(false);
            _isTyping = false;
        }

        /// <summary>
        /// DialogueManager から呼ばれるメイン表示メソッド
        /// </summary>
        /// <param name="speakerID">話者名</param>
        /// <param name="text">表示テキスト (タグ除去済み)</param>
        /// <param name="commands">パース済みコマンドリスト</param>
        /// <param name="onComplete">タイプライター完了時コールバック</param>
        public void DisplaySentence(string speakerID, string text, List<DialogueCommand> commands, Action onComplete)
        {
            _onCompleteCallback = onComplete;
            _pendingCommands = commands;

            if (_rootObject != null) _rootObject.SetActive(true);
            if (_speakerNameText != null) _speakerNameText.text = speakerID;
            if (_choicesContainer != null) _choicesContainer.SetActive(false);
            if (_continuePrompt != null) _continuePrompt.SetActive(false);

            _fullText = text;
            StartTypewriter();
        }

        /// <summary>
        /// 選択肢表示 (DialogueManager 現在の実装では ShowCurrentNode 内で直接 View に選択肢を出す処理はないが、インターフェースとして用意)
        /// ※現状 DialogueManager は選択肢を BranchDispatcher 経由で処理しているため、ここでは未使用だが実装しておく
        /// </summary>
        public void ShowChoices(IReadOnlyList<DialogueChoice> choices, Action<int> onChoiceSelected)
        {
            if (_choicesContainer == null || _choiceButtonPrefab == null) return;

            _choicesContainer.SetActive(true);
            if (_continuePrompt != null) _continuePrompt.SetActive(false);

            // 既存ボタン削除
            foreach (Transform child in _choicesContainer.transform)
                Destroy(child.gameObject);

            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                var btnObj = Instantiate(_choiceButtonPrefab, _choicesContainer.transform);
                var btnText = btnObj.GetComponentInChildren<TMP_Text>();
                if (btnText != null) btnText.text = choice.Text;

                int index = i; // クロージャ対策
                var btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() =>
                    {
                        onChoiceSelected?.Invoke(index);
                        _choicesContainer.SetActive(false);
                    });
                }
            }
        }

        public void ForceCompleteTyping()
        {
            if (!_isTyping) return;

            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
            }

            if (_dialogueText != null)
                _dialogueText.text = _fullText;

            _isTyping = false;
            OnTypingComplete();
        }

        public void CloseView()
        {
            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
            }
            _isTyping = false;

            if (_rootObject != null) _rootObject.SetActive(false);
            if (_choicesContainer != null) _choicesContainer.SetActive(false);
            if (_continuePrompt != null) _continuePrompt.SetActive(false);

            _onCompleteCallback = null;
            _pendingCommands = null;
        }

        // ---- 内部処理 ----

        private void StartTypewriter()
        {
            _isTyping = true;
            if (_dialogueText != null) _dialogueText.text = "";
            _typewriterCoroutine = StartCoroutine(TypewriterRoutine());
        }

        private System.Collections.IEnumerator TypewriterRoutine()
        {
            int charIndex = 0;
            while (charIndex < _fullText.Length)
            {
                if (_dialogueText != null)
                    _dialogueText.text = _fullText.Substring(0, charIndex + 1);

                charIndex++;
                yield return new WaitForSeconds(_typewriterSpeed);
            }

            _typewriterCoroutine = null;
            _isTyping = false;
            OnTypingComplete();
        }

        private void OnTypingComplete()
        {
            if (_continuePrompt != null) _continuePrompt.SetActive(true);

            // コマンド実行 (待機系コマンド等があればここで処理)
            if (_pendingCommands != null && _pendingCommands.Count > 0)
            {
                foreach (var cmd in _pendingCommands)
                {
                    // 例: [wait=1.0] 等のコマンド処理
                    // ここではログ出力のみ
                    Debug.Log($"[DialogueView] Command: {cmd.CommandType} Args: {string.Join(",", cmd.Arguments)}");
                }
            }

            _onCompleteCallback?.Invoke();
            _onCompleteCallback = null;
        }

        // ---- 入力受付 (Update で進める場合) ----
        // DialogueManager 側で Input を見ているため、View 側で入力待ちする必要は基本的にない。
        // 必要ならここで「クリックで ForceCompleteTyping / 次へ」を実装してもよい。
    }
}
