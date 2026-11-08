using System;
using System.Collections.Generic;
using UnityEngine;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Branching;
using Runtime.Dialogue;

namespace Runtime.Dialogue.Logic
{
    public enum DialogueState
    {
        Idle,
        Playing,
        WaitingForAdvance,
        WaitingForChoice,
        Ended
    }

    [AddComponentMenu("Dialogue/Core/Dialogue Manager")]
    [DisallowMultipleComponent]
    public class DialogueManager : MonoBehaviour
    {
        public string CurrentSpeaker => currentNode != null ? currentNode.speakerName : string.Empty;

        private static DialogueManager instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        public static DialogueManager Instance
        {
            get
            {
                if (instance == null || !instance)
                {
                    instance = FindFirstObjectByType<DialogueManager>();
                    if ((instance == null || !instance) && Application.isPlaying)
                    {
                        var go = new GameObject("[DialogueManager]");
                        instance = go.AddComponent<DialogueManager>();
                        if (go.transform.parent == null) DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        /// <summary>
        /// ノード再生が切り替わったときに発火するイベント（ノードID, ノードデータ）。Editorでのハイライト等に使用。
        /// </summary>
        public static event Action<string, DialogueNode> OnNodeStarted;

        /// <summary>
        /// 多言語化（ローカライズ）テキスト解決プロバイダー。
        /// Addressables や外部パッケージによる多言語辞書を差し込むことができます。
        /// 未設定時はノード記載のテキストがそのまま使用されます（完全非破壊）。
        /// </summary>
        public static IDialogueTextResolver TextResolver { get; set; }

        [SerializeField] private DialogueContainer currentContainer;
        [SerializeField, Tooltip("ダイアログUIのView。未指定の場合はシーン内から自動取得します")]
        private DialogueViewWindow defaultView;

        private IDialogueView currentView;
        public IDialogueView CurrentView
        {
            get
            {
                if (currentView == null)
                {
                    ResolveDefaultView();
                }
                return currentView;
            }
            private set => currentView = value;
        }

        public DialogueState CurrentState { get; private set; } = DialogueState.Idle;

        private DialogueNode currentNode;
        private Action onDialogueEndedCallback;

        [Header("Auto Mode Settings")]
        [SerializeField, Tooltip("オート再生時の基本待機時間（秒）")]
        private float baseAutoWaitTime = 1.0f;
        [SerializeField, Tooltip("1文字あたりの追加待機時間（秒）")]
        private float autoWaitTimePerChar = 0.05f;

        private bool isAutoMode = false;
        public bool IsAutoMode => isAutoMode;
        private Coroutine autoWaitCoroutine;

        private bool isSkipMode = false;
        public bool IsSkipMode => isSkipMode;

        // 外部入力待ち（方向選択・演出等）フラグ
        private bool isWaitingExternalInput = false;
        public bool IsWaitingExternalInput => isWaitingExternalInput;

        /// <summary>
        /// 現在話しかけている会話対象（NPCオブジェクト等）。
        /// 会話開始スクリプト（DialogueStarter等）から SetCurrentTarget または StartDialogue 経由で設定されます。
        /// </summary>
        public GameObject CurrentTarget { get; private set; }

        /// <summary>
        /// 会話対象のオブジェクトを設定します。
        /// </summary>
        public void SetCurrentTarget(GameObject target)
        {
            CurrentTarget = target;
        }

        private void Awake()
        {
            if (instance == null || !instance)
            {
                instance = this;
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            ResolveDefaultView();
        }

        private void Start()
        {
            if (currentView == null)
            {
                ResolveDefaultView();
            }
        }

        /// <summary>
        /// シーンまたはInspectorからIDialogueView（DialogueViewWindow）を自動解決します。
        /// </summary>
        public void ResolveDefaultView()
        {
            if (currentView != null) return;

            // 1. Inspectorで指定された defaultView があれば優先使用
            if (defaultView != null)
            {
                RegisterView(defaultView);
                return;
            }

            // 2. シーン内の DialogueViewWindow を検索（非アクティブも含む）
            var foundWindow = UnityEngine.Object.FindAnyObjectByType<DialogueViewWindow>(FindObjectsInactive.Include);
            if (foundWindow != null)
            {
                RegisterView(foundWindow);
                return;
            }

            // 3. DialogueViewWindow 以外の IDialogueView 実装コンポーネントを検索（非アクティブも含む）
            var allMonoBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mb in allMonoBehaviours)
            {
                if (mb is IDialogueView customView)
                {
                    RegisterView(customView);
                    return;
                }
            }
        }

        // 外部入力待ちのロック/解除
        public void SetExternalInputLock(bool locked)
        {
            isWaitingExternalInput = locked;
            if (locked)
            {
                DisableAutoMode();
                DisableSkipMode();
            }
        }

        public void Initialize(IDialogueView view)
        {
            CurrentView = view;
            CurrentView?.InitializeView();
        }

        public void RegisterView(IDialogueView view)
        {
            CurrentView = view;
            CurrentView?.InitializeView();
        }

        /// <summary>
        /// 会話相手のオブジェクトを指定して会話を開始します。
        /// </summary>
        /// <summary>
        /// Addressables アドレスを指定してコンテナを非同期ロードし、会話を開始します。
        /// </summary>
        /// <param name="containerAddress">Addressables のアドレス名</param>
        /// <param name="target">会話相手オブジェクト（任意）</param>
        /// <param name="onEnded">会話終了時コールバック</param>
        /// <param name="autoUnloadOnEnd">会話終了時に自動でコンテナをアンロード（メモリ解放）するかどうか</param>
        public async void StartDialogueWithAddress(string containerAddress, GameObject target = null, Action onEnded = null, bool autoUnloadOnEnd = false)
        {
            if (string.IsNullOrEmpty(containerAddress)) return;

            var container = await DialogueContainerManager.Instance.LoadContainerAsync(containerAddress);
            if (container != null)
            {
                Action wrappedCallback = () =>
                {
                    onEnded?.Invoke();
                    if (autoUnloadOnEnd)
                    {
                        DialogueContainerManager.Instance.UnloadContainer(containerAddress);
                    }
                };

                StartDialogue(container, target, wrappedCallback);
            }
        }

        public void StartDialogue(DialogueContainer container, GameObject target, Action onEnded = null)
        {
            CurrentTarget = target;
            StartDialogue(container, onEnded);
        }

        public void StartDialogue(DialogueContainer container, Action onEnded = null)
        {
            if (container != null)
            {
                currentContainer = container;
            }
            onDialogueEndedCallback = onEnded;
            StartDialogue(string.Empty);
        }

        public void StartDialogue(string startNodeID = "")
        {
            if (CurrentView == null)
            {
                ResolveDefaultView();
            }

            if (CurrentView == null)
            {
                Debug.LogWarning("[DialogueManager] 有効な IDialogueView（DialogueViewWindow等）が見つかりません。会話UIが表示されない可能性があります。");
            }

            if (currentContainer == null)
            {
                Debug.LogError("DialogueContainer が設定されていません！");
                CurrentState = DialogueState.Idle;
                return;
            }

            CurrentState = DialogueState.Playing;
            string targetID = string.IsNullOrEmpty(startNodeID) ? currentContainer.startNodeID : startNodeID;
            currentNode = currentContainer.GetNodeByID(targetID);

            if (currentNode == null)
            {
                Debug.LogError($"指定されたIDのノードが見つかりませんでした: {targetID}");
                EndDialogue();
                return;
            }

            ShowCurrentNode();
        }

        private void ShowCurrentNode()
        {
            if (currentNode == null)
            {
                EndDialogue();
                return;
            }

            OnNodeStarted?.Invoke(currentNode.nodeID, currentNode);

            string speakerID = currentNode.speakerName;
            string rawText = currentNode.dialogueText;

            // ローカライズリゾルバーが未設定の場合、シーン内の LocalizationManager から自動取得を試みる
            if (TextResolver == null)
            {
                var locMgr = UnityEngine.Object.FindFirstObjectByType<Runtime.Dialogue.Plugins.Localization.DialogueLocalizationManager>();
                if (locMgr != null)
                {
                    TextResolver = locMgr.TextResolver;
                }
            }

            // ローカライズリゾルバーが登録されていればテキスト・話者を解決
            if (TextResolver != null)
            {
                string orig = rawText;
                rawText = TextResolver.ResolveText(currentNode.nodeID, rawText);
                speakerID = TextResolver.ResolveSpeaker(speakerID);
                Debug.Log($"[DialogueManager] ローカライズ解決: Node={currentNode.nodeID}, 元文='{orig}' -> 解決後='{rawText}'");
            }
            else
            {
                Debug.LogWarning("[DialogueManager] TextResolver が null のためデフォルトテキストを使用します。");
            }

            string cleanText;
            List<DialogueCommand> commands;
            DialogueTagParser.ParseText(rawText, out cleanText, out commands);

            CurrentView?.DisplaySentence(speakerID, cleanText, commands, () =>
            {
                // 1. 選択肢が存在する場合の処理
                if (currentNode.choices != null && currentNode.choices.Count > 0)
                {
                    CurrentState = DialogueState.WaitingForChoice;

                    // 👇 選択肢が出た時はオート・スキップを解除して止める
                    DisableAutoMode();
                    DisableSkipMode();

                    if (DialogueBranchDispatcher.Instance != null)
                    {
                        var displayChoices = currentNode.choices;
                        if (TextResolver != null && currentNode.choices != null)
                        {
                            displayChoices = new List<ChoiceData>();
                            for (int i = 0; i < currentNode.choices.Count; i++)
                            {
                                var c = currentNode.choices[i];
                                if (c == null) continue;
                                string choiceKey = $"{currentNode.nodeID}#c{i}";
                                string resolved = TextResolver.ResolveChoice(choiceKey, c.choiceText);
                                if (resolved == c.choiceText && !string.IsNullOrEmpty(c.targetNodeID))
                                {
                                    resolved = TextResolver.ResolveChoice(c.targetNodeID, c.choiceText);
                                }
                                var copy = new ChoiceData
                                {
                                    choiceText = resolved,
                                    targetNodeID = c.targetNodeID,
                                    branchType = c.branchType,
                                    conditionKey = c.conditionKey,
                                    conditionOperator = c.conditionOperator,
                                    conditionValue = c.conditionValue
                                };
                                displayChoices.Add(copy);
                            }
                        }

                        bool handled = DialogueBranchDispatcher.Instance.TryHandleBranch(displayChoices, (nextID) =>
                        {
                            CurrentState = DialogueState.Playing;
                            StartDialogue(nextID);
                        });

                        if (!handled)
                        {
                            CurrentState = DialogueState.WaitingForAdvance;
                        }
                    }
                    else
                    {
                        CurrentState = DialogueState.WaitingForAdvance;
                    }
                }
                // 2. 選択肢がない場合（通常の文章）の処理
                else
                {
                    CurrentState = DialogueState.WaitingForAdvance;

                    // 外部入力待ち中でない場合のみオート/スキップを進行させる
                    if (!isWaitingExternalInput)
                    {
                        if (isSkipMode)
                        {
                            StartCoroutine(SkipAdvanceRoutine());
                        }
                        else if (isAutoMode)
                        {
                            StartAutoWait(cleanText.Length);
                        }
                    }
                }
            });

            if (isSkipMode && CurrentState == DialogueState.Playing)
            {
                CurrentView?.ForceCompleteTyping();
            }
        }

        public void HandleAdvanceInput()
        {
            // 外部入力待ち状態のときは進行入力を完全に無視する
            if (isWaitingExternalInput)
            {
                return;
            }

            if (autoWaitCoroutine != null)
            {
                StopCoroutine(autoWaitCoroutine);
                autoWaitCoroutine = null;
            }
            DisableAutoMode();
            DisableSkipMode();

            if (CurrentState == DialogueState.Playing)
            {
                CurrentView?.ForceCompleteTyping();
            }
            else if (CurrentState == DialogueState.WaitingForAdvance)
            {
                AdvanceToNextNode();
            }
        }

        private void AdvanceToNextNode()
        {
            if (!string.IsNullOrEmpty(currentNode.nextNodeID))
            {
                StartDialogue(currentNode.nextNodeID);
            }
            else
            {
                EndDialogue();
            }
        }

        public void EndDialogue()
        {
            CurrentState = DialogueState.Ended;
            CurrentView?.CloseView();

            Action callback = onDialogueEndedCallback;
            onDialogueEndedCallback = null;
            callback?.Invoke();

            CurrentState = DialogueState.Idle;

            // 会話が終わったらモードをオフにし、外部入力ロックも解除
            isAutoMode = false;
            isSkipMode = false;
            isWaitingExternalInput = false;
            CurrentTarget = null;
        }

        // --- Auto Mode ---
        public void ToggleAutoMode()
        {
            if (isWaitingExternalInput) return;

            isAutoMode = !isAutoMode;

            if (isAutoMode)
            {
                DisableSkipMode();

                if (CurrentState == DialogueState.WaitingForAdvance)
                {
                    StartAutoWait(0);
                }
            }
            else if (autoWaitCoroutine != null)
            {
                StopCoroutine(autoWaitCoroutine);
                autoWaitCoroutine = null;
            }
        }

        public void DisableAutoMode()
        {
            if (isAutoMode) isAutoMode = false;
        }

        private void StartAutoWait(int textLength)
        {
            if (autoWaitCoroutine != null) StopCoroutine(autoWaitCoroutine);
            float waitTime = baseAutoWaitTime + (textLength * autoWaitTimePerChar);
            autoWaitCoroutine = StartCoroutine(AutoWaitRoutine(waitTime));
        }

        private System.Collections.IEnumerator AutoWaitRoutine(float waitTime)
        {
            yield return new WaitForSeconds(waitTime);
            autoWaitCoroutine = null;
            if (!isWaitingExternalInput)
            {
                AdvanceToNextNode();
            }
        }

        // --- Skip Mode ---
        public void ToggleSkipMode()
        {
            if (isWaitingExternalInput) return;

            isSkipMode = !isSkipMode;

            if (isSkipMode)
            {
                DisableAutoMode();

                if (CurrentState == DialogueState.Playing)
                {
                    CurrentView?.ForceCompleteTyping();
                }
                else if (CurrentState == DialogueState.WaitingForAdvance)
                {
                    StartCoroutine(SkipAdvanceRoutine());
                }
            }
        }

        public void DisableSkipMode()
        {
            if (isSkipMode) isSkipMode = false;
        }

        private System.Collections.IEnumerator SkipAdvanceRoutine()
        {
            yield return null;

            if (isSkipMode && CurrentState == DialogueState.WaitingForAdvance && !isWaitingExternalInput)
            {
                AdvanceToNextNode();
            }
        }
    }
}
