using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Runtime.Dialogue;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;
using Runtime.Dialogue.Plugins.Portraits;
using Runtime.Dialogue.Plugins.Commands;
using DialoguePlugins.Layouts;

namespace Runtime.Dialogue.Plugins.Layouts
{
    /// <summary>
    /// レイアウト名と統合Prefabのマッピング定義
    /// </summary>
    [Serializable]
    public class CompositeLayoutMapping
    {
        [Tooltip("レイアウト識別キー (例: Novel, Retro, FullScreen, RPG)")]
        public string layoutName;

        [Tooltip("DialogueUIWindow と PortraitRoot を含む統合Prefab")]
        public GameObject compositePrefab;

        [Tooltip("切り替え時に、直前に表示されていた立ち絵（画像・表情・透明度）を新スロットへ自動引き継ぎするか")]
        public bool transferPortraitState = true;

        [Tooltip("切り替え時に、現在タイピング中・表示中のセリフテキストや話者名を新ウィンドウへ引き継ぐか")]
        public bool transferDialogueState = true;

        [Tooltip("切り替え時に、初期シーンに配置されていたデフォルトの DialogueUIWindow / PortraitRoot を非表示にするか")]
        public bool hideDefaultRoots = true;
    }

    /// <summary>
    /// 会話ウィンドウ（DialogueUIWindow）と立ち絵枠（PortraitRoot）を
    /// 1つのPrefabとして丸ごと一括差し替えするレイアウトハンドラー。
    /// 
    /// 【特徴】
    /// 1. 立ち絵スロット画像・透明度・モーションの自動引き継ぎ（スロット画像変更の不具合を防止）
    /// 2. SpriteSwapLayoutHandler（背景スプライト変更）との自動連動
    /// 3. セリフテキストや話者名のシームレスな転送
    /// 4. 生成インスタンスのキャッシュ・プールによるGCスパイク防止
    /// 5. [layout:name=default] による初期状態への完全復帰
    /// </summary>
    [HandlerInfo(
        description: "DialogueUIWindow と PortraitRoot を1つのPrefabとして丸ごとセットで動的に差し替える統合レイアウトハンドラーです。立ち絵スロット画像や背景スプライトの参照も自動で安全に再バインドされます。",
        usage: @"【基本パラメータ】
[layout:name=レイアウト名] または [layout:composite=レイアウト名]

【初期状態への復帰】
[layout:name=default] (または [layout:name=reset], [layout:name=initial])"
    )]
    [AddComponentMenu("Dialogue/Layout/Composite Dialogue Layout Handler")]
    public class CompositeDialogueLayoutHandler : MonoBehaviour, IDialogueLayoutHandler
    {
        [Tooltip("優先度。通常の枠差し替え(10)やプリセット(0)より優先して判定")]
        [SerializeField] private int priority = 25;
        public int Priority => priority;

        [Header("生成先コンテナ")]
        [Tooltip("生成した統合Prefabを配置する親Transform (未設定時はCanvas直下を自動検索)")]
        [SerializeField] private Transform mountParent;

        [Header("初期・デフォルト参照 (復帰用)")]
        [Tooltip("最初から配置されているデフォルトの会話ウィンドウ (DialogueUIWindow)")]
        [SerializeField] private GameObject defaultWindowRoot;

        [Tooltip("最初から配置されているデフォルトの立ち絵親 (PortraitRoot)")]
        [SerializeField] private GameObject defaultPortraitRoot;

        [Header("連携ハンドラー (未指定時は自動検索)")]
        [SerializeField] private StandardUIPortraitHandler portraitHandler;
        [SerializeField] private SpriteSwapLayoutHandler spriteSwapHandler;
        [SerializeField] private DialogueFadeCommandHandler fadeHandler;

        [Header("レイアウトPrefab登録リスト")]
        [SerializeField] private List<CompositeLayoutMapping> mappings = new List<CompositeLayoutMapping>();

        [Header("動作オプション")]
        [Tooltip("会話終了時（Idle時）に自動で初期デフォルトレイアウトへ復元するか")]
        [SerializeField] private bool restoreDefaultOnDialogueEnd = true;

        // 生成済みインスタンスのキャッシュ (layoutName -> GameObject)
        private Dictionary<string, GameObject> spawnedInstances = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private GameObject currentActiveInstance = null;
        private DialogueViewWindow defaultView = null;
        private bool isOverridden = false;
        private DialogueState prevDialogueState = DialogueState.Idle;

        private void Awake()
        {
            EnsureDefaultReferences();
        }

        private void Start()
        {
            if (DialogueLayoutDispatcher.Instance != null)
            {
                DialogueLayoutDispatcher.Instance.RegisterHandler(this);
            }
        }

        private void Update()
        {
            if (restoreDefaultOnDialogueEnd && isOverridden && DialogueManager.Instance != null)
            {
                DialogueState state = DialogueManager.Instance.CurrentState;
                if (state != prevDialogueState)
                {
                    if (state == DialogueState.Idle)
                    {
                        ResetToDefault();
                    }
                    prevDialogueState = state;
                }
            }
        }

        private void EnsureDefaultReferences()
        {
            if (mountParent == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null) mountParent = canvas.transform;
                else mountParent = transform.parent != null ? transform.parent : transform;
            }

            if (defaultWindowRoot == null)
            {
                var view = FindFirstObjectByType<DialogueViewWindow>();
                if (view != null)
                {
                    defaultWindowRoot = view.gameObject;
                    defaultView = view;
                }
            }
            else if (defaultView == null)
            {
                defaultView = defaultWindowRoot.GetComponentInChildren<DialogueViewWindow>(true);
            }

            if (defaultPortraitRoot == null)
            {
                Transform pTrans = transform.parent != null ? transform.parent.Find("PortraitRoot") : null;
                if (pTrans == null && mountParent != null) pTrans = mountParent.Find("PortraitRoot");
                if (pTrans != null) defaultPortraitRoot = pTrans.gameObject;
            }

            if (portraitHandler == null)
            {
                portraitHandler = FindFirstObjectByType<StandardUIPortraitHandler>();
            }

            if (spriteSwapHandler == null)
            {
                spriteSwapHandler = FindFirstObjectByType<SpriteSwapLayoutHandler>();
            }

            if (fadeHandler == null)
            {
                fadeHandler = FindFirstObjectByType<DialogueFadeCommandHandler>();
            }

            if (portraitHandler != null)
            {
                portraitHandler.SaveDefaultSlots();
            }

            if (spriteSwapHandler != null)
            {
                spriteSwapHandler.SaveDefaultTargetImage();
            }
        }

        public bool TryHandleLayout(string layoutName, Dictionary<string, string> args)
        {
            string targetKey = layoutName;
            if (args != null)
            {
                if (args.TryGetValue("composite", out string cVal) && !string.IsNullOrEmpty(cVal))
                {
                    targetKey = cVal;
                }
                else if (args.TryGetValue("prefab", out string pVal) && !string.IsNullOrEmpty(pVal))
                {
                    targetKey = pVal;
                }
                else if (args.TryGetValue("name", out string nVal) && !string.IsNullOrEmpty(nVal))
                {
                    targetKey = nVal;
                }
            }

            if (string.IsNullOrEmpty(targetKey)) return false;

            // 初期状態（デフォルト）への復元判定
            if (targetKey.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                targetKey.Equals("initial", StringComparison.OrdinalIgnoreCase) ||
                targetKey.Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                ResetToDefault();
                return true;
            }

            // 登録リストから該当レイアウトを検索
            var mapping = mappings.Find(m => m.layoutName.Equals(targetKey, StringComparison.OrdinalIgnoreCase));
            if (mapping == null || mapping.compositePrefab == null)
            {
                return false; // 他のレイアウトハンドラーへ処理を委譲
            }

            ApplyCompositeLayout(mapping);
            return true;
        }

        private void ApplyCompositeLayout(CompositeLayoutMapping mapping)
        {
            EnsureDefaultReferences();

            // 1. 直前の View の参照と状態をキャプチャ
            DialogueViewWindow oldView = DialogueManager.Instance?.CurrentView as DialogueViewWindow;
            if (oldView == null) oldView = defaultView;

            // 既に該当の統合レイアウトがアクティブであれば二重適用をスキップしてコルーチン中断を防止
            if (currentActiveInstance != null && currentActiveInstance.name == $"[Composite_{mapping.layoutName}]" && currentActiveInstance.activeSelf)
            {
                return;
            }

            // 2. 別の統合インスタンスがアクティブな場合は非アクティブ化
            if (currentActiveInstance != null)
            {
                currentActiveInstance.SetActive(false);
                currentActiveInstance = null;
            }

            // 3. 初期デフォルトルート（DialogueUIWindow / PortraitRoot）の非表示化
            // 統合レイアウトがアクティブな間はデフォルトのUI・立ち絵を非表示にして重複を防止
            if (defaultWindowRoot != null) defaultWindowRoot.SetActive(false);
            if (defaultPortraitRoot != null) defaultPortraitRoot.SetActive(false);

            // 4. キャッシュまたは新規インスタンスの取得
            GameObject targetObj = null;
            if (spawnedInstances.TryGetValue(mapping.layoutName, out var cachedObj) && cachedObj != null)
            {
                targetObj = cachedObj;
                targetObj.SetActive(true);
            }
            else
            {
                targetObj = Instantiate(mapping.compositePrefab, mountParent);
                targetObj.name = $"[Composite_{mapping.layoutName}]";

                targetObj.transform.localPosition = Vector3.zero;
                targetObj.transform.localRotation = Quaternion.identity;
                targetObj.transform.localScale = Vector3.one;

                if (targetObj.transform is RectTransform rt)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                }

                spawnedInstances[mapping.layoutName] = targetObj;
            }

            currentActiveInstance = targetObj;
            isOverridden = true;

            // 5. 新Prefab内の構成情報（Setup または自動検出）を解決
            var setup = targetObj.GetComponent<CompositeDialogueSetup>();
            if (setup == null)
            {
                setup = targetObj.AddComponent<CompositeDialogueSetup>();
            }
            setup.AutoDetectReferences();

            // 6. DialogueManager へ新しい DialogueViewWindow を登録 ＆ テキスト状態の引き継ぎ
            bool isPlaying = DialogueManager.Instance != null && DialogueManager.Instance.CurrentState == DialogueState.Playing;
            bool shouldTransferDialogue = mapping.transferDialogueState || isPlaying;

            if (setup.dialogueViewWindow != null)
            {
                // まず DialogueManager に新しい View を登録（ただしテキストは初期化・クリアさせない）
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.RegisterView(setup.dialogueViewWindow, resetView: false);
                }

                // 会話進行中、または明示的に引き継ぎ指定がある場合はセリフ状態を確実に引き継ぎ＆タイピング継続
                if (shouldTransferDialogue && oldView != null && oldView != setup.dialogueViewWindow)
                {
                    setup.dialogueViewWindow.TransferStateFrom(oldView, resumeTyping: true);
                }
                else
                {
                    // 念のため windowRoot と bodyText を確実に有効化
                    if (setup.dialogueViewWindow.windowRoot != null) setup.dialogueViewWindow.windowRoot.SetActive(true);
                    if (setup.dialogueViewWindow.BodyText != null) setup.dialogueViewWindow.BodyText.enabled = true;
                    if (setup.dialogueViewWindow.NameText != null) setup.dialogueViewWindow.NameText.enabled = true;
                }
            }

            // 7. 立ち絵スロットの再バインド ＆ 直前立ち絵画像の引き継ぎ（不具合防止）
            bool shouldTransferPortrait = mapping.transferPortraitState || true;
            if (portraitHandler != null && setup.portraitSlots != null && setup.portraitSlots.Count > 0)
            {
                portraitHandler.RebindSlots(setup.portraitSlots, shouldTransferPortrait);
            }

            // 8. SpriteSwapLayoutHandler（背景スプライト差し替え）との自動再バインド
            if (spriteSwapHandler != null && setup.backgroundTargetImage != null)
            {
                spriteSwapHandler.TargetImage = setup.backgroundTargetImage;
            }

            // 9. DialogueFadeCommandHandler のフェード対象を切り替え先のCanvasGroupに差し替え
            if (fadeHandler != null && setup.fadeCanvasGroup != null)
            {
                fadeHandler.SetTargetCanvasGroup(setup.fadeCanvasGroup);
            }

            Debug.Log($"<color=cyan>[CompositeDialogueLayoutHandler]</color> 統合レイアウト '{mapping.layoutName}' へ丸ごと差し替えました。（立ち絵引継: {shouldTransferPortrait}, テキスト引継: {shouldTransferDialogue}）");
        }

        /// <summary>
        /// 初期状態の DialogueUIWindow および PortraitRoot へ完全に復旧します。
        /// </summary>
        public void ResetToDefault()
        {
            if (!isOverridden && currentActiveInstance == null) return;

            // 1. アクティブな統合Prefabを非アクティブ化
            if (currentActiveInstance != null)
            {
                var activeSetup = currentActiveInstance.GetComponent<CompositeDialogueSetup>();
                DialogueViewWindow activeView = activeSetup != null ? activeSetup.dialogueViewWindow : currentActiveInstance.GetComponentInChildren<DialogueViewWindow>(true);

                // テキスト状態をデフォルトViewへ引き継ぎ
                if (activeView != null && defaultView != null && activeView != defaultView)
                {
                    defaultView.TransferStateFrom(activeView, resumeTyping: true);
                }

                currentActiveInstance.SetActive(false);
                currentActiveInstance = null;
            }

            // 2. デフォルトの DialogueUIWindow / PortraitRoot を再表示
            if (defaultWindowRoot != null) defaultWindowRoot.SetActive(true);
            if (defaultPortraitRoot != null) defaultPortraitRoot.SetActive(true);

            // 3. DialogueManager にデフォルトViewを再登録（テキストをクリアしない）
            if (defaultView != null && DialogueManager.Instance != null)
            {
                DialogueManager.Instance.RegisterView(defaultView, resetView: false);
            }

            // 4. 立ち絵スロットを初期配置へ復元（直前画像の引き継ぎも保持）
            if (portraitHandler != null)
            {
                portraitHandler.RestoreDefaultSlots(transferCurrentState: true);
            }

            // 5. SpriteSwapLayoutHandler を初期画像コンポーネントへ復元
            if (spriteSwapHandler != null)
            {
                spriteSwapHandler.RestoreDefaultTargetImage();
            }

            // 6. DialogueFadeCommandHandler のフェード対象をデフォルトに復元
            if (fadeHandler != null)
            {
                fadeHandler.RestoreDefaultCanvasGroup();
            }

            isOverridden = false;
            Debug.Log("<color=cyan>[CompositeDialogueLayoutHandler]</color> デフォルトの会話・立ち絵UIへ安全に復帰しました。");
        }
    }
}
