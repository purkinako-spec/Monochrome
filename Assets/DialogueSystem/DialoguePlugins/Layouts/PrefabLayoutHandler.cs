using System;
using System.Collections.Generic;
using UnityEngine;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

namespace Runtime.Dialogue.Plugins.Layouts
{
    /// <summary>
    /// 各レイアウト名と生成するPrefabのマッピング設定
    /// </summary>
    [Serializable]
    public class LayoutPrefabMapping
    {
        [Tooltip("レイアウト名・識別キー (例: RetroBox, Fantasy, Modern)")]
        public string layoutName;

        [Tooltip("動的に生成・配置する枠やUI装飾のPrefab")]
        public GameObject framePrefab;

        [Tooltip("このPrefabに切り替えた際、初期配置のデフォルト枠を非表示にするか")]
        public bool hideDefaultFrame = true;

        [Tooltip("テキスト領域(RectTransform)のオフセット調整が必要な場合はチェック")]
        public bool overrideTextRect = false;

        [Tooltip("テキスト枠の左下オフセット (Left, Bottom)")]
        public Vector2 textOffsetMin = Vector2.zero;

        [Tooltip("テキスト枠の右上オフセット (Right, Top)")]
        public Vector2 textOffsetMax = Vector2.zero;
    }

    /// <summary>
    /// 古い枠の差し替え動作モード
    /// </summary>
    public enum FrameReplaceMode
    {
        /// <summary>非アクティブ化してプール・保持（推奨: 参照切れや破棄による不都合を防ぎ、初期枠への復帰や再利用が高速）</summary>
        DeactivateAndCache,
        /// <summary>古い生成インスタンスをDestroyで破棄</summary>
        DestroyOld
    }

    /// <summary>
    /// ダイアログの枠や装飾画像をPrefab単位で動的に生成・差し替えるレイアウトハンドラー
    /// </summary>
    [HandlerInfo(
        description: "ダイアログの枠や装飾画像をPrefab単位で動的に生成・差し替えるレイアウトハンドラーです。初期枠への安全な復帰やインスタンスのキャッシュにも対応しています。",
        usage: @"【基本パラメータ】
[layout:name=レイアウト名] または [layout:prefab=Prefab登録名]

【初期状態への復帰】
[layout:name=default] (または [layout:name=reset], [layout:name=initial])"
    )]
    public class PrefabLayoutHandler : MonoBehaviour, IDialogueLayoutHandler
    {
        [Tooltip("優先度。既存の StandardUIPresetLayoutHandler(0) より優先して判定")]
        [SerializeField] private int priority = 10;
        public int Priority => priority;

        [Header("生成先コンテナ")]
        [Tooltip("生成したPrefabを配置する親Transform (未設定時はこのコンポーネント自身のTransformを使用)")]
        [SerializeField] private Transform frameMountParent;

        [Header("初期・デフォルト枠 (復帰用)")]
        [Tooltip("最初からシーンに配置されているデフォルトの枠・背景オブジェクト (初期状態復帰時に再表示)")]
        [SerializeField] private GameObject defaultFrameRoot;

        [Header("差し替え動作設定")]
        [Tooltip("別のPrefabに切り替える際の古いインスタンスの扱い (非アクティブ化保持 または 破棄)")]
        [SerializeField] private FrameReplaceMode replaceMode = FrameReplaceMode.DeactivateAndCache;

        [Header("テキスト領域 (任意)")]
        [Tooltip("Prefab差し替えに合わせてテキスト領域の余白・位置を調整したい場合に指定")]
        [SerializeField] private RectTransform textRectTransform;

        [Header("登録Prefabリスト")]
        [SerializeField] private List<LayoutPrefabMapping> prefabMappings = new List<LayoutPrefabMapping>();

        // 生成済みインスタンスのキャッシュ (layoutName -> GameObject)
        private Dictionary<string, GameObject> spawnedInstances = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private GameObject currentActiveInstance = null;

        private Vector2 originalTextOffsetMin;
        private Vector2 originalTextOffsetMax;
        private bool hasSavedOriginalTextRect = false;

        private void Awake()
        {
            if (frameMountParent == null)
            {
                frameMountParent = transform;
            }

            if (textRectTransform != null && !hasSavedOriginalTextRect)
            {
                originalTextOffsetMin = textRectTransform.offsetMin;
                originalTextOffsetMax = textRectTransform.offsetMax;
                hasSavedOriginalTextRect = true;
            }
        }

        private void Start()
        {
            if (DialogueLayoutDispatcher.Instance != null)
            {
                DialogueLayoutDispatcher.Instance.RegisterHandler(this);
            }
        }

        public bool TryHandleLayout(string layoutName, Dictionary<string, string> args)
        {
            // 引数に "prefab" が指定されている場合はそちらを優先、なければ layoutName
            string targetKey = layoutName;
            if (args != null)
            {
                if (args.TryGetValue("prefab", out string pVal) && !string.IsNullOrEmpty(pVal))
                {
                    targetKey = pVal;
                }
                else if (args.TryGetValue("name", out string nVal) && !string.IsNullOrEmpty(nVal))
                {
                    targetKey = nVal;
                }
            }

            if (string.IsNullOrEmpty(targetKey)) return false;

            // 初期状態（デフォルト）への復帰判定
            if (targetKey.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                targetKey.Equals("initial", StringComparison.OrdinalIgnoreCase) ||
                targetKey.Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                ResetToDefault();
                return true;
            }

            // マッピングリストから合致するものを検索
            var mapping = prefabMappings.Find(m => m.layoutName.Equals(targetKey, StringComparison.OrdinalIgnoreCase));
            if (mapping == null || mapping.framePrefab == null)
            {
                // このハンドラーに登録がない名前の場合は false を返し、既存の他のハンドラーへ処理を委譲
                return false;
            }

            ApplyPrefabLayout(mapping);
            return true;
        }

        private void ApplyPrefabLayout(LayoutPrefabMapping mapping)
        {
            // 1. 初期デフォルト枠の表示・非表示
            if (defaultFrameRoot != null)
            {
                defaultFrameRoot.SetActive(!mapping.hideDefaultFrame);
            }

            // 2. 現在アクティブな生成物の非アクティブ化または破棄
            if (currentActiveInstance != null)
            {
                if (replaceMode == FrameReplaceMode.DestroyOld)
                {
                    string removeKey = null;
                    foreach (var pair in spawnedInstances)
                    {
                        if (pair.Value == currentActiveInstance)
                        {
                            removeKey = pair.Key;
                            break;
                        }
                    }
                    if (removeKey != null) spawnedInstances.Remove(removeKey);
                    Destroy(currentActiveInstance);
                    currentActiveInstance = null;
                }
                else
                {
                    currentActiveInstance.SetActive(false);
                    currentActiveInstance = null;
                }
            }

            // 3. 対象Prefabの取得（キャッシュ優先）または新規生成
            GameObject targetObj = null;
            if (replaceMode == FrameReplaceMode.DeactivateAndCache && spawnedInstances.TryGetValue(mapping.layoutName, out var cachedObj))
            {
                if (cachedObj != null)
                {
                    targetObj = cachedObj;
                    targetObj.SetActive(true);
                }
            }

            if (targetObj == null)
            {
                targetObj = Instantiate(mapping.framePrefab, frameMountParent);
                if (targetObj.transform is RectTransform rect)
                {
                    rect.localPosition = Vector3.zero;
                    rect.localRotation = Quaternion.identity;
                    rect.localScale = Vector3.one;
                }

                if (replaceMode == FrameReplaceMode.DeactivateAndCache)
                {
                    spawnedInstances[mapping.layoutName] = targetObj;
                }
            }

            currentActiveInstance = targetObj;

            // 4. テキスト領域のオフセット調整（必要な場合）
            if (textRectTransform != null)
            {
                if (mapping.overrideTextRect)
                {
                    textRectTransform.offsetMin = mapping.textOffsetMin;
                    textRectTransform.offsetMax = mapping.textOffsetMax;
                }
                else if (hasSavedOriginalTextRect)
                {
                    textRectTransform.offsetMin = originalTextOffsetMin;
                    textRectTransform.offsetMax = originalTextOffsetMax;
                }
            }
        }

        /// <summary>
        /// 初期状態の枠に安全に復元する
        /// </summary>
        public void ResetToDefault()
        {
            if (currentActiveInstance != null)
            {
                if (replaceMode == FrameReplaceMode.DestroyOld)
                {
                    Destroy(currentActiveInstance);
                }
                else
                {
                    currentActiveInstance.SetActive(false);
                }
                currentActiveInstance = null;
            }

            if (defaultFrameRoot != null)
            {
                defaultFrameRoot.SetActive(true);
            }

            if (textRectTransform != null && hasSavedOriginalTextRect)
            {
                textRectTransform.offsetMin = originalTextOffsetMin;
                textRectTransform.offsetMax = originalTextOffsetMax;
            }
        }
    }
}
