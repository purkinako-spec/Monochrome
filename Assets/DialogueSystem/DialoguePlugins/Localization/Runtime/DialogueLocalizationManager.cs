using System;
using System.Threading.Tasks;
using UnityEngine;
using Runtime.Dialogue;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Runtime.Dialogue.Plugins.Localization
{
    /// <summary>
    /// ゲーム全体の多言語テキスト・フォント切り替えを一括統括するマネージャー。
    /// （非破壊設計: DialogueManager.TextResolver に自動登録し、UIフォントも連動して切り替えます）
    /// </summary>
    [AddComponentMenu("Dialogue/Localization/Dialogue Localization Manager")]
    [DisallowMultipleComponent]
    public class DialogueLocalizationManager : MonoBehaviour
    {
        private static DialogueLocalizationManager instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        public static DialogueLocalizationManager Instance
        {
            get
            {
                if (instance == null || !instance)
                {
                    instance = FindFirstObjectByType<DialogueLocalizationManager>();
                    if ((instance == null || !instance) && Application.isPlaying)
                    {
                        var go = new GameObject("[DialogueLocalizationManager]");
                        instance = go.AddComponent<DialogueLocalizationManager>();
                        instance.EnsureInitialized();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        [Header("Configuration")]
        [SerializeField, Tooltip("言語設定マスターアセット")]
        private DialogueLocalizationConfig config;

        [SerializeField, Tooltip("起動時に DialogueManager.TextResolver へ自動登録するかどうか")]
        private bool autoRegisterResolver = true;

        [Header("Debug / Status")]
        [SerializeField] private string currentLanguageCode = "";
        [SerializeField] private bool isLoading = false;

        public string CurrentLanguageCode => currentLanguageCode;
        public bool IsLoading => isLoading;
        public DialogueLocalizationConfig Config => config;

        /// <summary>
        /// 言語が切り替わった時に通知されるイベント（引数は新しい言語コード）
        /// </summary>
        public event Action<string> OnLanguageChanged;

        private ILanguageAssetLoader assetLoader;
        private LocalizationTextResolver textResolver;
        private LoadedLanguageAssets currentAssets;

        public LocalizationTextResolver TextResolver
        {
            get
            {
                EnsureInitialized();
                return textResolver;
            }
        }

        public void EnsureInitialized()
        {
            if (assetLoader == null) assetLoader = new AddressablesLanguageLoader();
            if (textResolver == null) textResolver = new LocalizationTextResolver();

            if (autoRegisterResolver && (DialogueManager.TextResolver == null || (object)DialogueManager.TextResolver != (object)textResolver))
            {
                DialogueManager.TextResolver = textResolver;
            }

            if (config == null)
            {
                TryAutoAssignConfig();
            }
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

            EnsureInitialized();
        }

        private async void Start()
        {
            EnsureInitialized();

            if (config != null && !string.IsNullOrEmpty(config.DefaultLanguageCode))
            {
                // まだ言語が読み込まれていない場合のみデフォルト言語を適用
                if (string.IsNullOrEmpty(currentLanguageCode))
                {
                    await ChangeLanguageAsync(config.DefaultLanguageCode);
                }
            }
        }

        public void TryAutoAssignConfig()
        {
            if (config != null) return;

#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:DialogueLocalizationConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                config = AssetDatabase.LoadAssetAtPath<DialogueLocalizationConfig>(path);
                if (config != null)
                {
                    Debug.Log($"[DialogueLocalizationManager] DialogueLocalizationConfig を自動検出して割り当てました: {path}");
                }
            }
#endif
        }

        /// <summary>
        /// 設定マスターアセットを動的に割り当てます
        /// </summary>
        public void SetConfig(DialogueLocalizationConfig newConfig)
        {
            config = newConfig;
        }

        /// <summary>
        /// アセットローダーを差し替えます（Addressables 対応ローダー等を外部から注入可能）
        /// </summary>
        public void SetAssetLoader(ILanguageAssetLoader loader)
        {
            if (loader != null)
            {
                assetLoader = loader;
            }
        }

        /// <summary>
        /// 指定された言語コードに切り替えます（非同期ロード対応）
        /// </summary>
        public async Task<bool> ChangeLanguageAsync(string targetLanguageCode)
        {
            EnsureInitialized();

            if (isLoading)
            {
                Debug.LogWarning("[DialogueLocalizationManager] 現在別の言語をロード中です。");
                return false;
            }

            if (config == null)
            {
                TryAutoAssignConfig();
            }

            if (config == null)
            {
                Debug.LogError("[DialogueLocalizationManager] LocalizationConfig が設定されていません。Inspector で割り当てるか作成してください。");
                return false;
            }

            var langConfig = config.GetConfig(targetLanguageCode);
            if (langConfig == null)
            {
                Debug.LogWarning($"[DialogueLocalizationManager] サポートされていない言語コードです: {targetLanguageCode}");
                return false;
            }

            isLoading = true;
            try
            {
                // 1. 新しい言語のアセット（テキスト辞書・フォントDB）をロード
                var newAssets = await assetLoader.LoadAssetsAsync(langConfig);
                if (newAssets == null)
                {
                    Debug.LogError($"[DialogueLocalizationManager] 言語アセットのロードに失敗しました: {targetLanguageCode}");
                    return false;
                }

                // 2. 以前のアセットを解放
                if (currentAssets != null)
                {
                    assetLoader.ReleaseAssets(currentAssets);
                }

                currentAssets = newAssets;
                currentLanguageCode = targetLanguageCode;

                // 3. テキストリゾルバーへ反映
                if (textResolver == null) textResolver = new LocalizationTextResolver();
                textResolver.SetTable(newAssets.TextTable);
                DialogueManager.TextResolver = textResolver;

                // 4. フォントデータベースを UI View に反映
                if (newAssets.FontDatabase != null)
                {
                    ApplyFontDatabaseToViews(newAssets.FontDatabase);
                }

                // 5. 言語変更通知イベント発火
                OnLanguageChanged?.Invoke(currentLanguageCode);
                Debug.Log($"[DialogueLocalizationManager] 言語を '{targetLanguageCode}' ({langConfig.displayName}) に切り替えました。Table={(newAssets.TextTable != null ? newAssets.TextTable.name : "null(Default)")}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
            finally
            {
                isLoading = false;
            }
        }

        /// <summary>
        /// シーン内のすべての DialogueViewWindow に新しいフォントデータベースを適用します
        /// </summary>
        private void ApplyFontDatabaseToViews(DialogueFontDatabase fontDB)
        {
            if (fontDB == null) return;

            var views = FindObjectsByType<DialogueViewWindow>(FindObjectsSortMode.None);
            foreach (var v in views)
            {
                if (v != null)
                {
                    v.SetFontDatabase(fontDB);
                }
            }
        }
    }
}
