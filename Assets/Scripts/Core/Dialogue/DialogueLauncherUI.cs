using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using Runtime.Dialogue;
using Runtime.Dialogue.Audio;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;
using Runtime.Dialogue.Plugins.Localization;
using Monochrome.Core.Input;
using Monochrome.Core.Character;

namespace Monochrome.Core.Dialogue
{
    /// <summary>
    /// UIランチャーから会話（Dialogue）を実行するための選択肢エントリ。
    /// - 配列・リストで管理され、スクリプトから動的に追加・削除・変更が可能。
    /// - Addressables アドレスまたは直接アセット指定に対応。
    /// - 多言語化（DialogueTextTable）および FlagManager 条件判定に対応。
    /// </summary>
    [Serializable]
    public class DialogueLaunchEntry
    {
        [Tooltip("一意識別ID（スクリプトから取得・有効化・削除する際に使用）")]
        public string id = "talk_1";

        [Tooltip("多言語化テーブル (DialogueTextTable) で参照するキー（未指定時は defaultTitle）")]
        public string titleKey = "";

        [Tooltip("ボタンに表示するデフォルトタイトル（翻訳が見つからない場合のフォールバック）")]
        public string defaultTitle = "会話を開始";

        [Header("Target Dialogue (Addressables or Direct Asset)")]
        [Tooltip("Addressables のアドレス名 (例: 'murabito', 'FishingMiniGameDialogueContainer')")]
        public string dialogueContainerAddress = "";

        [Tooltip("直接アサインするDialogueContainer (未設定時はAddressablesを使用)")]
        public DialogueContainer directContainer = null;

        [Tooltip("開始ノードID（空欄なら最初から）")]
        public string startNodeId = "";

        [Tooltip("会話終了時に自動でコンテナをアンロード（メモリ解放）するか")]
        public bool autoUnloadOnEnd = false;

        [Header("Conditions")]
        [Tooltip("表示・実行するためのフラグ条件（FlagManager。未設定なら常に有効）")]
        public List<FlagCondition> conditions = new List<FlagCondition>();

        [Tooltip("この選択肢が有効かどうか（外部からオンオフ可能）")]
        public bool isEnabled = true;

        public DialogueLaunchEntry() { }

        public DialogueLaunchEntry(string id, string defaultTitle, string dialogueContainerAddress, string titleKey = "")
        {
            this.id = id;
            this.defaultTitle = defaultTitle;
            this.dialogueContainerAddress = dialogueContainerAddress;
            this.titleKey = titleKey;
            this.isEnabled = true;
        }
    }

    /// <summary>
    /// UIからいつでもDialogue（会話）を起動できる常駐型UIランチャーマネージャー。
    /// 
    /// 【特徴・スクロール動作】
    /// 1. パネル内に配置されたボタン（スロットボタン）の数で、一度に表示する選択肢数を決定。
    /// 2. ScrollChoiceHandler と同様に、選択肢がスロット枠を超えると 1,2,3 -> 2,3,4 のようにスロットの内容がウィンドウシフトして切り替わる。
    /// 3. 端まで行った時は一番下から上、一番上から下へループ可能。
    /// 4. 選択中ボタンの横にはカーソル（▶）が追従表示。
    /// 5. タッチタップで直接選択、タッチドラッグ/マウスホイール/キーボード/パッド入力でシフトスクロールが可能。
    /// 6. 開始ボタンの表示/非表示（SetLauncherButtonVisible）、有効/無効化（SetLauncherButtonInteractable）対応。
    /// 7. 会話中は自動で開始ボタンを非表示化または操作不能にロック。
    /// 8. Addressables（DialogueContainerManager）および多言語化（DialogueLocalizationManager）と連携。
    /// </summary>
    [AddComponentMenu("Monochrome/Dialogue/Dialogue Launcher UI")]
    [DisallowMultipleComponent]
    public class DialogueLauncherUI : MonoBehaviour
    {
        private static DialogueLauncherUI instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        public static DialogueLauncherUI Instance
        {
            get
            {
                if (instance == null || !instance)
                {
                    instance = FindFirstObjectByType<DialogueLauncherUI>();
                    if ((instance == null || !instance) && Application.isPlaying)
                    {
                        var go = new GameObject("[DialogueLauncherUI]");
                        instance = go.AddComponent<DialogueLauncherUI>();
                        if (go.transform.parent == null) DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        [Header("Dialogue Entries (選択肢リスト)")]
        [SerializeField, Tooltip("登録されている会話選択肢のリスト（Inspectorでの初期定義およびランタイム変更可能）")]
        private List<DialogueLaunchEntry> entries = new List<DialogueLaunchEntry>();

        [Header("UI Elements (開始ボタン & パネル)")]
        [SerializeField, Tooltip("常時表示される開始ボタン（未指定時は自動生成）")]
        private Button launcherButton;

        [SerializeField, Tooltip("開始ボタンのラベルテキスト（プロンプト案内用）")]
        private TextMeshProUGUI launcherButtonText;

        [SerializeField, Tooltip("開始ボタンのCanvasGroup（表示/非表示・フェード用）")]
        private CanvasGroup launcherButtonCanvasGroup;

        [SerializeField, Tooltip("選択肢を表示するモーダルパネル（未指定時は自動生成）")]
        private GameObject selectionModalPanel;

        [SerializeField, Tooltip("選択肢ボタンを配置する親Transform（Content）")]
        private Transform contentContainer;

        [SerializeField, Tooltip("選択肢ボタンPrefab（スロット自動補完用）")]
        private GameObject choiceButtonPrefab;

        [SerializeField, Tooltip("選択中カーソルオブジェクト（未指定時は'▶'を自動生成）")]
        private GameObject cursorObject;

        [SerializeField, Tooltip("閉じるボタン（任意）")]
        private Button closeButton;

        [SerializeField, Tooltip("会話システムPrefab（シーン内にDialogueViewWindowが無い場合の自動ロード用）")]
        private GameObject dialogueSystemPrefab;

        [Header("Slot Buttons (配置されたボタンによるスクロール制御)")]
        [SerializeField, Tooltip("パネル内に配置されたスロットボタン群。配置されたボタンの数で一度に表示される選択肢数が決まります（未指定時はパネル内から自動収集）")]
        private List<Button> slotButtons = new List<Button>();

        [Header("Scroll Indicators (任意)")]
        [SerializeField, Tooltip("上にスクロールできるときに表示する Image。スクロール不可なら非表示になります。")]
        private Image scrollUpIndicator;

        [SerializeField, Tooltip("下にスクロールできるときに表示する Image。スクロール不可なら非表示になります。")]
        private Image scrollDownIndicator;

        [Header("Slot Visuals (任意)")]
        [SerializeField, Tooltip("選択中ボタンスプライト")]
        private Sprite selectedSprite;

        [SerializeField, Tooltip("通常時ボタンスプライト")]
        private Sprite normalSprite;

        [SerializeField, Tooltip("選択中テキストカラー")]
        private Color selectedTextColor = Color.yellow;

        [SerializeField, Tooltip("通常時テキストカラー")]
        private Color normalTextColor = Color.white;

        [SerializeField, Tooltip("カーソルの選択中ボタンに対する相対オフセット位置")]
        private Vector2 cursorOffset = new Vector2(-40f, 0f);

        [Header("Input System (自由に変更可能な入力対応)")]
        [SerializeField, Tooltip("ランチャーを開く/閉じるアクション（未指定時はInputSystem_Actionsから自動検索）")]
        private InputActionReference openAction;

        [SerializeField, Tooltip("選択肢の上下移動アクション（未指定時はUI/NavigateまたはPlayer/Move）")]
        private InputActionReference navigateAction;

        [SerializeField, Tooltip("選択決定アクション（未指定時はUI/Submit）")]
        private InputActionReference submitAction;

        [SerializeField, Tooltip("キャンセル・戻るアクション（未指定時はUI/Cancel）")]
        private InputActionReference cancelAction;

        [SerializeField, Tooltip("論理キー名 (InputPromptResolver用。例: 'Talk', 'Menu')")]
        private string logicalKey = "Talk";

        [SerializeField, Tooltip("アクション説明（プロンプト用。例: '会話'）")]
        private string actionDescription = "会話";

        [Header("Behavior Settings")]
        [SerializeField, Tooltip("会話実行中に開始ボタンを自動で非表示/無効化するか")]
        private bool autoHideDuringDialogue = true;

        [SerializeField, Tooltip("パネル表示中や会話中にプレイヤーの移動をロックするか")]
        private bool autoBlockPlayerMovement = true;

        [SerializeField, Tooltip("端まで移動したときに上下ループするか（1番上から上で最下部へ、最下部から下で1番上へ）")]
        private bool loopNavigation = true;

        [SerializeField, Tooltip("長押し時の連続スクロール遅延（秒）")]
        private float repeatDelay = 0.2f;

        [SerializeField, Tooltip("シーン遷移時も破棄せず常駐するか（DontDestroyOnLoad）")]
        private bool persistAcrossScenes = true;

        [Header("Audio Settings")]
        [SerializeField, Tooltip("カーソル移動SEキー")]
        private string cursorSEKey = "cursor_move";

        [SerializeField, Tooltip("選択決定SEキー")]
        private string submitSEKey = "choice_select";

        [SerializeField, Tooltip("キャンセルSEキー")]
        private string cancelSEKey = "cancel";

        // ランタイム状態管理
        private bool isButtonVisible = true;
        private bool isButtonInteractable = true;
        private bool isOpen = false;
        private int currentIndex = 0;  // 全選択肢中の選択インデックス (0 .. N-1)
        private int windowStart = 0;   // スロット先頭に表示されている選択肢インデックス
        private List<DialogueLaunchEntry> currentAvailableEntries = new List<DialogueLaunchEntry>();
        private GameObject spawnedCursor;
        private Coroutine inputLoopCoroutine;
        private DialogueState prevDialogueState = DialogueState.Idle;

        public bool IsOpen => isOpen;
        public bool IsLauncherButtonVisible => isButtonVisible;
        public bool IsLauncherButtonInteractable => isButtonInteractable;
        public IReadOnlyList<DialogueLaunchEntry> Entries => entries;
        public IReadOnlyList<Button> SlotButtons => slotButtons;

        private void Awake()
        {
            if (instance == null || !instance)
            {
                instance = this;
                if (persistAcrossScenes && transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureEventSystem();
            EnsureUIHierarchy();
            CollectSlotButtons();
            AutoBindInputActions();
        }

        private void Start()
        {
            // 初期状態はパネル非表示
            if (selectionModalPanel != null)
            {
                selectionModalPanel.SetActive(false);
            }

            // 開始ボタン・閉じるボタンのリスナー設定
            if (launcherButton != null)
            {
                launcherButton.onClick.RemoveAllListeners();
                launcherButton.onClick.AddListener(ToggleLauncher);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseLauncher);
            }

            // プロンプトとボタン表示の初期更新
            UpdatePromptText();

            // 言語変更・デバイス変更イベントの購読
            if (DialogueLocalizationManager.Instance != null)
            {
                DialogueLocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
            }

            if (InputDeviceManager.Instance != null)
            {
                InputDeviceManager.Instance.OnDeviceChanged += HandleDeviceChanged;
            }

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            var loc = DialogueLocalizationManager.Instance;
            if (loc != null)
            {
                loc.OnLanguageChanged -= HandleLanguageChanged;
            }

            var input = InputDeviceManager.Instance;
            if (input != null)
            {
                input.OnDeviceChanged -= HandleDeviceChanged;
            }

            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;

            DisableActions();
        }

        private void Update()
        {
            // 会話ステートの監視（会話中なら自動で開始ボタンを隠す/操作不能にする）
            if (autoHideDuringDialogue && DialogueManager.Instance != null)
            {
                DialogueState currentState = DialogueManager.Instance.CurrentState;
                if (currentState != prevDialogueState)
                {
                    if (currentState != DialogueState.Idle)
                    {
                        // 会話開始時：もし選択パネルが開いていたら閉じる
                        if (isOpen) CloseLauncher();
                        ApplyButtonVisibility(false);
                    }
                    else
                    {
                        // 会話終了時：元の表示状態に戻す
                        ApplyButtonVisibility(isButtonVisible);
                        ApplyButtonInteractable(isButtonInteractable);
                    }
                    prevDialogueState = currentState;
                }
            }

            // ランチャー起動の入力判定（メニューが開いていないとき）
            if (!isOpen)
            {
                if (isButtonVisible && isButtonInteractable)
                {
                    if (CheckOpenInput())
                    {
                        OpenLauncher();
                    }
                }
            }
        }

        // ==========================================
        // 🎮 開始ボタンの表示・操作制御 API
        // ==========================================

        /// <summary>
        /// 開始ボタンを押せるか（interactable）を切り替えます。
        /// </summary>
        public void SetLauncherButtonInteractable(bool interactable)
        {
            isButtonInteractable = interactable;
            ApplyButtonInteractable(interactable);
        }

        /// <summary>
        /// 開始ボタン自体の表示・非表示（見えなくする）を切り替えます。
        /// </summary>
        public void SetLauncherButtonVisible(bool visible)
        {
            isButtonVisible = visible;
            ApplyButtonVisibility(visible);
        }

        private void ApplyButtonInteractable(bool interactable)
        {
            if (launcherButton != null)
            {
                launcherButton.interactable = interactable;
            }
            if (launcherButtonCanvasGroup != null)
            {
                launcherButtonCanvasGroup.interactable = interactable;
                launcherButtonCanvasGroup.blocksRaycasts = interactable;
            }
        }

        private void ApplyButtonVisibility(bool visible)
        {
            if (launcherButtonCanvasGroup != null)
            {
                launcherButtonCanvasGroup.alpha = visible ? 1f : 0f;
                launcherButtonCanvasGroup.blocksRaycasts = visible && isButtonInteractable;
                launcherButtonCanvasGroup.interactable = visible && isButtonInteractable;
            }
            else if (launcherButton != null)
            {
                launcherButton.gameObject.SetActive(visible);
            }
        }

        // ==========================================
        // 📜 選択肢リストの動的変更・追加 API
        // ==========================================

        /// <summary>
        /// 選択肢を1件追加します（同IDが存在する場合は上書き更新）。
        /// </summary>
        public void AddEntry(DialogueLaunchEntry entry)
        {
            if (entry == null) return;
            int idx = entries.FindIndex(e => e.id.Equals(entry.id, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                entries[idx] = entry;
            }
            else
            {
                entries.Add(entry);
            }

            if (isOpen) RefreshChoicesUI();
        }

        /// <summary>
        /// 指定IDの選択肢を削除します。
        /// </summary>
        public bool RemoveEntry(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            int count = entries.RemoveAll(e => e.id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (count > 0 && isOpen)
            {
                RefreshChoicesUI();
            }
            return count > 0;
        }

        /// <summary>
        /// 指定IDの選択肢の有効/無効を切り替えます。
        /// </summary>
        public void SetEntryEnabled(string id, bool enabled)
        {
            var entry = GetEntry(id);
            if (entry != null)
            {
                entry.isEnabled = enabled;
                if (isOpen) RefreshChoicesUI();
            }
        }

        /// <summary>
        /// 指定IDの選択肢データを取得します。
        /// </summary>
        public DialogueLaunchEntry GetEntry(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return entries.Find(e => e.id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 全選択肢を一括置換します。
        /// </summary>
        public void SetEntries(IEnumerable<DialogueLaunchEntry> newEntries)
        {
            entries.Clear();
            if (newEntries != null)
            {
                entries.AddRange(newEntries);
            }
            if (isOpen) RefreshChoicesUI();
        }

        /// <summary>
        /// 全選択肢をクリアします。
        /// </summary>
        public void ClearEntries()
        {
            entries.Clear();
            if (isOpen) RefreshChoicesUI();
        }

        // ==========================================
        // 🪟 ランチャーウィンドウの開閉
        // ==========================================

        public void ToggleLauncher()
        {
            if (isOpen) CloseLauncher();
            else OpenLauncher();
        }

        public void OpenLauncher()
        {
            if (isOpen) return;
            if (!isButtonInteractable) return;

            // 会話中なら開けない
            if (DialogueManager.Instance != null && DialogueManager.Instance.CurrentState != DialogueState.Idle)
            {
                return;
            }

            isOpen = true;
            if (selectionModalPanel != null)
            {
                selectionModalPanel.SetActive(true);
            }

            // プレイヤーの操作を一時ロック
            if (autoBlockPlayerMovement && PlayerController2D.LocalPlayer != null)
            {
                PlayerController2D.LocalPlayer.BlockState.AddBlocker(ActionBlocker.Menu);
            }

            // パネル内のスロットボタンを収集
            CollectSlotButtons();

            // フラグ条件で有効な選択肢を抽出
            var flags = FlagManager.Instance;
            currentAvailableEntries = entries.Where(e =>
                e != null && e.isEnabled &&
                (e.conditions == null || e.conditions.Count == 0 || flags == null || flags.EvaluateAll(e.conditions))
            ).ToList();

            // ScrollChoiceHandler と同様に状態初期化
            currentIndex = 0;
            windowStart = 0;

            // レイアウト確定後にスロット描画（VerticalLayoutGroupの計算を待つため1フレーム遅延）
            if (inputLoopCoroutine != null) StopCoroutine(inputLoopCoroutine);
            inputLoopCoroutine = StartCoroutine(OpenLauncherRoutine());
        }

        public void CloseLauncher()
        {
            if (!isOpen) return;

            isOpen = false;
            if (selectionModalPanel != null)
            {
                selectionModalPanel.SetActive(false);
            }

            if (inputLoopCoroutine != null)
            {
                StopCoroutine(inputLoopCoroutine);
                inputLoopCoroutine = null;
            }

            // プレイヤーの操作ロックを解除
            if (autoBlockPlayerMovement && PlayerController2D.LocalPlayer != null)
            {
                PlayerController2D.LocalPlayer.BlockState.RemoveBlocker(ActionBlocker.Menu);
            }

            if (spawnedCursor != null)
            {
                spawnedCursor.SetActive(false);
            }

            // パネルを閉じたらインジケーターも非表示
            if (scrollUpIndicator != null)   scrollUpIndicator.gameObject.SetActive(false);
            if (scrollDownIndicator != null) scrollDownIndicator.gameObject.SetActive(false);
        }

        // ==========================================
        // 🔘 スロットボタン収集
        // ==========================================

        /// <summary>
        /// Prefab またはパネル内に配置されたボタンを収集し、画面の上から下（Y座標降順）にソートします。
        /// 配置されたボタンの数がスロット数（スクロールするまでの表示数）になります。
        /// </summary>
        public void CollectSlotButtons()
        {
            slotButtons.RemoveAll(b => b == null);

            if (slotButtons.Count == 0)
            {
                Transform searchRoot = contentContainer != null ? contentContainer : (selectionModalPanel != null ? selectionModalPanel.transform : transform);
                var found = searchRoot.GetComponentsInChildren<Button>(true)
                    .Where(b => b != launcherButton && b != closeButton)
                    .OrderByDescending(b => b.transform.position.y)
                    .ToList();
                slotButtons.AddRange(found);
            }

            // Prefab等でボタンが全く配置されていない場合の自動生成フォールバック
            if (slotButtons.Count == 0 && contentContainer != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    var btn = CreateChoiceButtonInstance($"SlotButton_{i + 1}");
                    if (btn != null) slotButtons.Add(btn);
                }
            }
        }

        // ==========================================
        // 📋 スロット描画 & ウィンドウシフト (ScrollChoiceHandler準拠)
        // ==========================================

        private void RefreshChoicesUI()
        {
            var flags = FlagManager.Instance;
            currentAvailableEntries = entries.Where(e =>
                e != null && e.isEnabled &&
                (e.conditions == null || e.conditions.Count == 0 || flags == null || flags.EvaluateAll(e.conditions))
            ).ToList();

            CollectSlotButtons();

            int total = currentAvailableEntries.Count;
            int slotCount = slotButtons.Count;

            currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, total - 1));
            if (currentIndex < windowStart)
            {
                windowStart = currentIndex;
            }
            if (slotCount > 0 && currentIndex >= windowStart + slotCount)
            {
                windowStart = Mathf.Max(0, currentIndex - slotCount + 1);
            }

            RefreshSlots(playSE: false);
        }

        /// <summary>
        /// スロットのテキスト、ボタンリスナー、カーソル位置を更新（ウィンドウシフト）
        /// ScrollChoiceHandler と同様のロジックで動作します。
        /// </summary>
        private void RefreshSlots(bool playSE)
        {
            if (playSE && !string.IsNullOrEmpty(cursorSEKey))
            {
                DialogueAudioManager.Instance?.PlaySE(cursorSEKey);
            }

            int slotCount = slotButtons.Count;
            int total = currentAvailableEntries.Count;

            EnsureCursor();

            for (int i = 0; i < slotCount; i++)
            {
                Button btn = slotButtons[i];
                if (btn == null) continue;

                int choiceIdx = windowStart + i;

                if (choiceIdx < total)
                {
                    btn.gameObject.SetActive(true);

                    var entry = currentAvailableEntries[choiceIdx];
                    string resolvedTitle = ResolveEntryTitle(entry);

                    var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text = resolvedTitle;
                    }
                    else
                    {
                        var legacyText = btn.GetComponentInChildren<Text>();
                        if (legacyText != null) legacyText.text = resolvedTitle;
                    }

                    // クリック/タップリスナー設定（タッチですぐ選択可能）
                    int targetChoiceIdx = choiceIdx;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        currentIndex = targetChoiceIdx;
                        ExecuteEntry(entry);
                    });

                    // ホバーイベント（マウス移動での選択同期）
                    SetupHoverEvent(btn.gameObject, targetChoiceIdx);

                    // 選択状態のビジュアル反映
                    bool isSelected = (choiceIdx == currentIndex);

                    var img = btn.GetComponent<Image>();
                    if (img != null)
                    {
                        if (isSelected && selectedSprite != null) img.sprite = selectedSprite;
                        else if (!isSelected && normalSprite != null) img.sprite = normalSprite;
                    }

                    if (tmp != null) tmp.color = isSelected ? selectedTextColor : normalTextColor;

                    // カーソル位置を更新
                    if (isSelected && spawnedCursor != null)
                    {
                        spawnedCursor.SetActive(true);
                        spawnedCursor.transform.position = btn.transform.position + (Vector3)cursorOffset;
                    }
                }
                else
                {
                    // 選択肢数がスロット数未満の場合、余りスロットは非表示
                    btn.gameObject.SetActive(false);
                }
            }

            // EventSystem の選択状態も同期
            int activeSlotIdx = currentIndex - windowStart;
            if (activeSlotIdx >= 0 && activeSlotIdx < slotButtons.Count && EventSystem.current != null)
            {
                if (InputDeviceManager.Instance == null || InputDeviceManager.Instance.CurrentDeviceType != InputDeviceType.Touchscreen)
                {
                    UINavigationHelper.Focus(slotButtons[activeSlotIdx].gameObject);
                }
            }

            if (total == 0 && spawnedCursor != null)
            {
                spawnedCursor.SetActive(false);
            }

            // スクロールインジケーターの表示切り替え
            UpdateScrollIndicators(total, slotCount);
        }

        /// <summary>
        /// 上下スクロールインジケーター（Image）の表示・非表示を更新します。
        /// ・上にスクロールできる（windowStart > 0）→ scrollUpIndicator を表示
        /// ・下にスクロールできる（windowStart + slotCount < total）→ scrollDownIndicator を表示
        /// ・スクロール不可（選択肢がスロット数に収まる）→ 両方非表示
        /// </summary>
        private void UpdateScrollIndicators(int total, int slotCount)
        {
            bool canScrollUp   = windowStart > 0;
            bool canScrollDown = slotCount > 0 && (windowStart + slotCount) < total;

            if (scrollUpIndicator != null)
            {
                scrollUpIndicator.gameObject.SetActive(canScrollUp);
            }

            if (scrollDownIndicator != null)
            {
                scrollDownIndicator.gameObject.SetActive(canScrollDown);
            }
        }

        /// <summary>
        /// 下移動（1,2,3 -> 2,3,4 のウィンドウシフト ＆ 末尾から先頭へのループ）
        /// ScrollChoiceHandler と同様のアルゴリズム
        /// </summary>
        public void MoveDown()
        {
            int total = currentAvailableEntries.Count;
            int slotCount = slotButtons.Count;
            if (total <= 1) return;

            if (currentIndex < total - 1)
            {
                currentIndex++;

                // スロット枠の下端を超えたらウィンドウを1つ下へシフト (1,2,3 -> 2,3,4)
                if (slotCount > 0 && currentIndex >= windowStart + slotCount)
                {
                    windowStart = currentIndex - slotCount + 1;
                }
            }
            else if (loopNavigation)
            {
                // 一番下からさらに下 -> 一番上（先頭）へループ！
                currentIndex = 0;
                windowStart = 0;
            }

            RefreshSlots(playSE: true);
        }

        /// <summary>
        /// 上移動（先頭から末尾へのループ ＆ ウィンドウシフト）
        /// ScrollChoiceHandler と同様のアルゴリズム
        /// </summary>
        public void MoveUp()
        {
            int total = currentAvailableEntries.Count;
            int slotCount = slotButtons.Count;
            if (total <= 1) return;

            if (currentIndex > 0)
            {
                currentIndex--;

                // スロット枠の上端を超えたらウィンドウを1つ上へシフト
                if (currentIndex < windowStart)
                {
                    windowStart = currentIndex;
                }
            }
            else if (loopNavigation)
            {
                // 一番上からさらに上 -> 一番下（末尾）へループ！
                currentIndex = total - 1;
                windowStart = Mathf.Max(0, total - slotCount);
            }

            RefreshSlots(playSE: true);
        }

        private Button CreateChoiceButtonInstance(string name = "ChoiceButton")
        {
            GameObject obj = null;
            if (choiceButtonPrefab != null)
            {
                obj = Instantiate(choiceButtonPrefab, contentContainer);
                obj.name = name;
            }
            else
            {
                obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(contentContainer, false);

                var img = obj.GetComponent<Image>();
                img.color = new Color(0.2f, 0.2f, 0.22f, 0.95f);

                var rt = obj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(360f, 48f);

                var textObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(obj.transform, false);

                var trt = textObj.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.sizeDelta = Vector2.zero;
                trt.offsetMin = new Vector2(35f, 0f);
                trt.offsetMax = new Vector2(-15f, 0f);

                var tmp = textObj.GetComponent<TextMeshProUGUI>();
                tmp.fontSize = 22;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
            }

            obj.transform.localScale = Vector3.one;
            return obj.GetComponent<Button>();
        }

        private void SetupHoverEvent(GameObject target, int index)
        {
            var trigger = target.GetComponent<EventTrigger>();
            if (trigger == null) trigger = target.AddComponent<EventTrigger>();

            trigger.triggers.RemoveAll(e => e.eventID == EventTriggerType.PointerEnter);

            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener((_) =>
            {
                if (InputDeviceManager.Instance != null && InputDeviceManager.Instance.CurrentDeviceType == InputDeviceType.Touchscreen)
                {
                    return; // タッチ操作中はホバーによるカーソル移動をスキップ
                }
                if (currentIndex != index)
                {
                    currentIndex = index;
                    RefreshSlots(playSE: true);
                }
            });
            trigger.triggers.Add(entry);
        }

        private void EnsureCursor()
        {
            if (spawnedCursor != null) return;

            if (cursorObject != null)
            {
                spawnedCursor = Instantiate(cursorObject, selectionModalPanel.transform);
                spawnedCursor.SetActive(true);
                return;
            }

            Transform existingCursor = selectionModalPanel.transform.Find("Cursor");
            if (existingCursor != null)
            {
                spawnedCursor = existingCursor.gameObject;
                spawnedCursor.SetActive(true);
                return;
            }

            // なければデフォルトカーソル（TMPの▶）を生成
            var autoCursor = new GameObject("LauncherCursor", typeof(RectTransform));
            autoCursor.transform.SetParent(selectionModalPanel.transform, false);

            var rt = autoCursor.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(30f, 30f);

            var tmp = autoCursor.AddComponent<TextMeshProUGUI>();
            tmp.text = "▶";
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = selectedTextColor;
            tmp.raycastTarget = false;

            spawnedCursor = autoCursor;
            spawnedCursor.SetActive(true);
        }

        // ==========================================
        // 🕹️ ボタン・キーボード・パッドの入力ループ
        // ==========================================

        /// <summary>
        /// パネルを開いた直後、VerticalLayoutGroup のレイアウト確定を1フレーム待ってから
        /// カーソル位置を正しく描画し、入力ループへ移行するルーチン。
        /// </summary>
        private IEnumerator OpenLauncherRoutine()
        {
            // 1フレーム待機してレイアウト確定させる
            yield return null;

            // レイアウト確定後に Canvas を強制更新してスロット初期描画
            Canvas.ForceUpdateCanvases();
            RefreshSlots(playSE: false);

            // 入力ループへ
            yield return StartCoroutine(SelectionInputLoop());
        }

        private IEnumerator SelectionInputLoop()
        {
            yield return null; // 開いた瞬間の入力誤爆防止

            float verticalTimer = 0f;

            while (isOpen)
            {
                // 1. 上下スクロール・ナビゲーション入力
                float v = GetVerticalInput();

                if (Mathf.Abs(v) > 0.5f)
                {
                    if (verticalTimer <= 0f)
                    {
                        if (v > 0f) MoveUp();     // 上へ
                        else if (v < 0f) MoveDown(); // 下へ

                        verticalTimer = repeatDelay;
                    }
                    else
                    {
                        verticalTimer -= Time.unscaledDeltaTime;
                    }
                }
                else
                {
                    verticalTimer = 0f;
                }

                // マウスホイールでのスクロール（ホイール上=上移動、ホイール下=下移動）
                float wheel = UnityEngine.Input.mouseScrollDelta.y;
                if (wheel > 0.2f)
                {
                    MoveUp();
                }
                else if (wheel < -0.2f)
                {
                    MoveDown();
                }

                // 2. 決定入力
                if (CheckSubmitInput())
                {
                    if (currentIndex >= 0 && currentIndex < currentAvailableEntries.Count)
                    {
                        ExecuteEntry(currentAvailableEntries[currentIndex]);
                        yield break;
                    }
                }

                // 3. キャンセル入力
                if (CheckCancelInput())
                {
                    if (!string.IsNullOrEmpty(cancelSEKey))
                    {
                        DialogueAudioManager.Instance?.PlaySE(cancelSEKey);
                    }
                    CloseLauncher();
                    yield break;
                }

                yield return null;
            }
        }

        // ==========================================
        // 💬 会話実行 (Addressables & DialogueManager)
        // ==========================================

        private async void ExecuteEntry(DialogueLaunchEntry entry)
        {
            if (entry == null) return;

            if (!string.IsNullOrEmpty(submitSEKey))
            {
                DialogueAudioManager.Instance?.PlaySE(submitSEKey);
            }

            CloseLauncher();

            // シーン内に DialogueViewWindow がなければ自動生成・解決
            EnsureDialogueViewAvailable();

            var container = await ResolveContainerAsync(entry);
            if (container == null)
            {
                Debug.LogWarning($"[DialogueLauncherUI] 会話コンテナが見つかりません。Entry ID: {entry.id}, Address: {entry.dialogueContainerAddress}", this);
                return;
            }

            // プレイヤーの操作を会話ロック
            if (PlayerController2D.LocalPlayer != null)
            {
                PlayerController2D.LocalPlayer.BlockState.AddBlocker(ActionBlocker.Dialogue);
            }

            Action onDialogueFinished = () =>
            {
                if (PlayerController2D.LocalPlayer != null)
                {
                    PlayerController2D.LocalPlayer.BlockState.RemoveBlocker(ActionBlocker.Dialogue);
                }

                if (entry.autoUnloadOnEnd && !string.IsNullOrEmpty(entry.dialogueContainerAddress))
                {
                    DialogueContainerManager.Instance?.UnloadContainer(entry.dialogueContainerAddress);
                }

                Debug.Log($"[DialogueLauncherUI] 会話が終了しました: {entry.id}");
            };

            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue(container, gameObject, onDialogueFinished);
            }
        }

        private async Task<DialogueContainer> ResolveContainerAsync(DialogueLaunchEntry entry)
        {
            if (entry == null) return null;

            // 1. 直接指定コンテナがあれば最優先
            if (entry.directContainer != null)
            {
                return entry.directContainer;
            }

            // 2. Addressables から非同期ロード（既存の DialogueContainerManager をフル活用）
            if (!string.IsNullOrEmpty(entry.dialogueContainerAddress))
            {
                if (DialogueContainerManager.Instance != null)
                {
                    return await DialogueContainerManager.Instance.LoadContainerAsync(entry.dialogueContainerAddress);
                }
            }

            return null;
        }

        // ==========================================
        // 🌐 多言語化 & プロンプト解決
        // ==========================================

        private string ResolveEntryTitle(DialogueLaunchEntry entry)
        {
            if (entry == null) return string.Empty;

            string fallback = string.IsNullOrEmpty(entry.defaultTitle) ? entry.id : entry.defaultTitle;

            if (!string.IsNullOrEmpty(entry.titleKey) && DialogueLocalizationManager.Instance != null && DialogueLocalizationManager.Instance.TextResolver != null)
            {
                return DialogueLocalizationManager.Instance.TextResolver.ResolveChoice(entry.titleKey, fallback);
            }

            return fallback;
        }

        private void UpdatePromptText()
        {
            if (launcherButtonText == null) return;

#if ENABLE_INPUT_SYSTEM
            // openAction が Inspector 等で設定されている場合、その割り当てキー（例: [T], [Tab] 等）を優先表示
            if (openAction != null && openAction.action != null)
            {
                var currentDevice = InputDeviceManager.Instance != null ? InputDeviceManager.Instance.CurrentDeviceType : InputDeviceType.KeyboardMouse;
                if (currentDevice == InputDeviceType.Touchscreen)
                {
                    launcherButtonText.text = actionDescription;
                    return;
                }

                string bindingStr = openAction.action.GetBindingDisplayString();
                if (!string.IsNullOrEmpty(bindingStr))
                {
                    launcherButtonText.text = $"[{bindingStr}] {actionDescription}";
                    return;
                }
            }
#endif

            launcherButtonText.text = InputPromptResolver.GetPromptText(logicalKey, actionDescription);
        }

        private void HandleLanguageChanged(string newLang)
        {
            UpdatePromptText();
            if (isOpen)
            {
                RefreshChoicesUI();
            }
        }

        private void HandleDeviceChanged(InputDeviceType newDevice)
        {
            UpdatePromptText();
            if (isOpen && newDevice == InputDeviceType.Touchscreen)
            {
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }

        // ==========================================
        // ⚙️ シーン遷移 & 自動UI生成 (全シーン対応)
        // ==========================================

        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            EnsureEventSystem();
            EnsureDialogueViewAvailable();
            UpdatePromptText();
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                esGo.AddComponent<StandaloneInputModule>();
#endif
                if (transform.parent == null) DontDestroyOnLoad(esGo);
            }
        }

        private void EnsureDialogueViewAvailable()
        {
            if (DialogueManager.Instance == null) return;

            if (DialogueManager.Instance.CurrentView == null)
            {
                DialogueManager.Instance.ResolveDefaultView();
            }

            // シーン内に DialogueViewWindow が見当たらない場合、Prefabから自動生成
            if (DialogueManager.Instance.CurrentView == null && dialogueSystemPrefab != null)
            {
                var spawned = Instantiate(dialogueSystemPrefab);
                spawned.name = dialogueSystemPrefab.name;
                var view = spawned.GetComponentInChildren<DialogueViewWindow>(true);
                if (view != null)
                {
                    DialogueManager.Instance.RegisterView(view);
                    Debug.Log("<color=cyan>[DialogueLauncherUI]</color> シーン内に会話UIが見つからなかったため、Prefabから自動生成しました。");
                }
            }
        }

        private void EnsureUIHierarchy()
        {
            // Canvas またはコンポーネントがInspectorで未設定の場合、動的生成
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 90;

                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                gameObject.AddComponent<GraphicRaycaster>();
            }

            // 1. 開始ボタンの自動生成（未指定時）
            if (launcherButton == null)
            {
                var btnObj = new GameObject("LauncherButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(CanvasGroup));
                btnObj.transform.SetParent(transform, false);

                var rt = btnObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-40f, 40f);
                rt.sizeDelta = new Vector2(180f, 54f);

                var img = btnObj.GetComponent<Image>();
                img.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);

                launcherButton = btnObj.GetComponent<Button>();
                launcherButtonCanvasGroup = btnObj.GetComponent<CanvasGroup>();

                var textObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(btnObj.transform, false);

                var trt = textObj.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.sizeDelta = Vector2.zero;

                launcherButtonText = textObj.GetComponent<TextMeshProUGUI>();
                launcherButtonText.fontSize = 24;
                launcherButtonText.alignment = TextAlignmentOptions.Center;
                launcherButtonText.color = Color.white;
            }

            // 2. 選択肢モーダルパネルの自動生成（未指定時）
            if (selectionModalPanel == null)
            {
                selectionModalPanel = new GameObject("SelectionModalPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                selectionModalPanel.transform.SetParent(transform, false);

                var rt = selectionModalPanel.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(440f, 320f);

                var bg = selectionModalPanel.GetComponent<Image>();
                bg.color = new Color(0.08f, 0.08f, 0.1f, 0.96f);

                // ヘッダータイトル
                var headerObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                headerObj.transform.SetParent(selectionModalPanel.transform, false);
                var hrt = headerObj.GetComponent<RectTransform>();
                hrt.anchorMin = new Vector2(0f, 1f);
                hrt.anchorMax = new Vector2(1f, 1f);
                hrt.pivot = new Vector2(0.5f, 1f);
                hrt.anchoredPosition = new Vector2(0f, -15f);
                hrt.sizeDelta = new Vector2(0f, 40f);
                var htmp = headerObj.GetComponent<TextMeshProUGUI>();
                htmp.text = "会話を選択";
                htmp.fontSize = 26;
                htmp.alignment = TextAlignmentOptions.Center;
                htmp.color = Color.white;

                // 閉じるボタン
                var closeObj = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                closeObj.transform.SetParent(selectionModalPanel.transform, false);
                var crt = closeObj.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(1f, 1f);
                crt.anchorMax = new Vector2(1f, 1f);
                crt.pivot = new Vector2(1f, 1f);
                crt.anchoredPosition = new Vector2(-15f, -15f);
                crt.sizeDelta = new Vector2(36f, 36f);
                closeObj.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f, 0.8f);
                closeButton = closeObj.GetComponent<Button>();

                var closeText = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                closeText.transform.SetParent(closeObj.transform, false);
                var ctrt = closeText.GetComponent<RectTransform>();
                ctrt.anchorMin = Vector2.zero;
                ctrt.anchorMax = Vector2.one;
                ctrt.sizeDelta = Vector2.zero;
                var ctmp = closeText.GetComponent<TextMeshProUGUI>();
                ctmp.text = "✕";
                ctmp.fontSize = 20;
                ctmp.alignment = TextAlignmentOptions.Center;
                ctmp.color = Color.white;

                // Content (スロットボタンを並べる親)
                var contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
                contentObj.transform.SetParent(selectionModalPanel.transform, false);
                var contRt = contentObj.GetComponent<RectTransform>();
                contRt.anchorMin = new Vector2(0.05f, 0.05f);
                contRt.anchorMax = new Vector2(0.95f, 0.82f);
                contRt.sizeDelta = Vector2.zero;

                var vlg = contentObj.GetComponent<VerticalLayoutGroup>();
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.spacing = 10f;
                vlg.padding = new RectOffset(35, 15, 10, 10);

                contentContainer = contentObj.transform;
            }
        }

        // ==========================================
        // 🎮 入力アクション自動バインド & 判定
        // ==========================================

        private void AutoBindInputActions()
        {
#if ENABLE_INPUT_SYSTEM
            // InputSystem_Actions アセットから Player/Interact・UI/Navigate 等を自動バインド
            var assets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
            foreach (var asset in assets)
            {
                if (asset == null || !asset.name.Contains("InputSystem")) continue;

                var uiMap = asset.FindActionMap("UI");
                if (uiMap != null)
                {
                    if (navigateAction == null)
                    {
                        var a = uiMap.FindAction("Navigate");
                        if (a != null) navigateAction = InputActionReference.Create(a);
                    }
                    if (submitAction == null)
                    {
                        var a = uiMap.FindAction("Submit");
                        if (a != null) submitAction = InputActionReference.Create(a);
                    }
                    if (cancelAction == null)
                    {
                        var a = uiMap.FindAction("Cancel");
                        if (a != null) cancelAction = InputActionReference.Create(a);
                    }
                }

                // Player マップから logicalKey と同名のアクション（未設定時は検索）
                var playerMap = asset.FindActionMap("Player");
                if (playerMap != null)
                {
                    if (openAction == null && !string.IsNullOrEmpty(logicalKey))
                    {
                        // logicalKey（例: "Talk" 等）と一致するアクションのみ検索
                        // ※ 勝手に "Interact" を割り当てて通常インタラクトと誤爆するのを防止
                        var a = playerMap.FindAction(logicalKey);
                        if (a != null) openAction = InputActionReference.Create(a);
                    }
                }
                break;
            }

            EnableActions();

            // InputDeviceManager が存在すれば初回のプロンプト表示を更新
            // （デバイス変更時は HandleDeviceChanged → UpdatePromptText が自動で走る）
            UpdatePromptText();
#endif
        }

        private void EnableActions()
        {
#if ENABLE_INPUT_SYSTEM
            if (openAction != null && openAction.action != null) openAction.action.Enable();
            if (navigateAction != null && navigateAction.action != null) navigateAction.action.Enable();
            if (submitAction != null && submitAction.action != null) submitAction.action.Enable();
            if (cancelAction != null && cancelAction.action != null) cancelAction.action.Enable();
#endif
        }

        private void DisableActions()
        {
#if ENABLE_INPUT_SYSTEM
            if (openAction != null && openAction.action != null) openAction.action.Disable();
            if (navigateAction != null && navigateAction.action != null) navigateAction.action.Disable();
            if (submitAction != null && submitAction.action != null) submitAction.action.Disable();
            if (cancelAction != null && cancelAction.action != null) cancelAction.action.Disable();
#endif
        }

        private bool CheckOpenInput()
        {
#if ENABLE_INPUT_SYSTEM
            // ① openAction が設定されている場合：
            //    そのアクションの入力のみ判定し、結果をそのまま返す（他キーやInteractへの誤爆を完全に防止）
            if (openAction != null && openAction.action != null)
            {
                return openAction.action.WasPressedThisFrame();
            }
#endif
            // ② openAction が未設定の場合は false を返し、勝手に Interact (Fキー等) で開かないようにする
            return false;
        }

        private float GetVerticalInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (navigateAction != null && navigateAction.action != null)
            {
                Vector2 nav = navigateAction.action.ReadValue<Vector2>();
                if (Mathf.Abs(nav.y) > 0.2f) return nav.y;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.isPressed || kb.wKey.isPressed) return 1f;
                if (kb.downArrowKey.isPressed || kb.sKey.isPressed) return -1f;
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.dpad.up.isPressed || gp.leftStick.up.isPressed) return 1f;
                if (gp.dpad.down.isPressed || gp.leftStick.down.isPressed) return -1f;
            }
#endif
            return UnityEngine.Input.GetAxisRaw("Vertical");
        }

        private bool CheckSubmitInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (submitAction != null && submitAction.action != null && submitAction.action.WasPressedThisFrame())
            {
                return true;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                {
                    return true;
                }
            }

            var gp = Gamepad.current;
            if (gp != null && gp.buttonSouth.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.Space);
        }

        private bool CheckCancelInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (cancelAction != null && cancelAction.action != null && cancelAction.action.WasPressedThisFrame())
            {
                return true;
            }

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                return true;
            }

            var gp = Gamepad.current;
            if (gp != null && gp.buttonEast.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return UnityEngine.Input.GetKeyDown(KeyCode.Escape);
        }
    }
}
