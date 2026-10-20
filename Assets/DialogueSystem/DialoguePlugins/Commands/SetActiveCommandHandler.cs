using Runtime.Dialogue.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Dialogue.Commands
{
    /// <summary>
    /// インスペクターから事前登録するためのマッピングクラス
    /// </summary>
    [Serializable]
    public class ActiveCommandTarget
    {
        [Tooltip("会話タグで指定する識別キー (例: SecretDoor)")]
        public string key;
        [Tooltip("切り替える対象の GameObject")]
        public GameObject targetObj;
    }

    [HandlerInfo(
    description: @"指定したオブジェクトのアクティブ状態（表示・非表示）を、柔軟な指定方法（名前・タグ・インスペクター登録）で制御するコマンドハンドラーです。",
    usage: @"【基本パラメータ】
[active:mode=指定方法, target=対象, state=T/F]

【パラメータ詳細】
・mode   : 検索方法 (name / tag / direct)。省略時は name
・target : 対象オブジェクト名 / タグ名 / インスペクター登録キー
・state  : T(True) で表示(アクティブ)、F(False) で非表示(非アクティブ)

【使用例】
・名前で指定: [active:mode=name,target=Wall_Right,state=F] (または [active:target=Wall_Right,state=F])
・タグ名で指定: [active:mode=tag,target=Enemy,state=F]
・インスペクター登録名で指定: [active:mode=direct,target=SecretDoor,state=T]"
    )]
    public class SetActiveCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "active";

        [Header("指定方法3 (mode=direct) 用: Inspector登録リスト")]
        [Tooltip("会話イベントで確実に操作したいオブジェクト（特に非アクティブのもの）を事前登録します。")]
        [SerializeField] private List<ActiveCommandTarget> directTargets = new List<ActiveCommandTarget>();

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            ApplyActiveState(command);
            onComplete?.Invoke();
        }

        public void ForceComplete(DialogueCommand command)
        {
            // スキップ時も確実に状態を適用
            ApplyActiveState(command);
        }

        private void ApplyActiveState(DialogueCommand command)
        {
            // mode が指定されていない場合は "name" をデフォルトとする
            string mode = command.GetString("mode", "name").ToLower();
            string targetParam = command.GetString("target", "");
            string stateStr = command.GetString("state", "T");

            if (string.IsNullOrEmpty(targetParam))
            {
                Debug.LogWarning("[SetActiveCommandHandler] target パラメータが指定されていません。");
                return;
            }

            bool isActive = stateStr.Equals("T", StringComparison.OrdinalIgnoreCase) ||
                            stateStr.Equals("True", StringComparison.OrdinalIgnoreCase);

            switch (mode)
            {
                case "name":
                    SetByName(targetParam, isActive);
                    break;
                case "tag":
                    SetByTag(targetParam, isActive);
                    break;
                case "direct":
                    SetByDirect(targetParam, isActive);
                    break;
                default:
                    Debug.LogWarning($"[SetActiveCommandHandler] 不正な mode 指定です: {mode}");
                    break;
            }
        }

        // ==========================================
        // 1. 名前で検索 (mode=name)
        // ==========================================
        private void SetByName(string name, bool isActive)
        {
            GameObject targetObj = FindGameObjectIncludingInactive(name);
            if (targetObj != null)
            {
                targetObj.SetActive(isActive);
            }
            else
            {
                Debug.LogWarning($"[SetActiveCommandHandler] 名前 '{name}' のオブジェクトが見つかりませんでした。");
            }
        }

        // ==========================================
        // 2. タグ名で検索 (mode=tag)
        // ==========================================
        private void SetByTag(string tagName, bool isActive)
        {
            Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            bool found = false;
            foreach (Transform t in allTransforms)
            {
                if (t.gameObject.hideFlags == HideFlags.None && t.gameObject.CompareTag(tagName))
                {
                    t.gameObject.SetActive(isActive);
                    found = true;
                }
            }

            if (!found)
            {
                Debug.LogWarning($"[SetActiveCommandHandler] タグ '{tagName}' を持つシーン内オブジェクトが見つかりませんでした。");
            }
        }

        // ==========================================
        // 3. インスペクター登録名で検索 (mode=direct)
        // ==========================================
        private void SetByDirect(string key, bool isActive)
        {
            var target = directTargets.Find(x => x.key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (target != null && target.targetObj != null)
            {
                target.targetObj.SetActive(isActive);
            }
            else
            {
                Debug.LogWarning($"[SetActiveCommandHandler] direct 指定キー '{key}' がインスペクターに未登録か、オブジェクトが空です。");
            }
        }

        // --- 非アクティブなオブジェクトも検索可能なヘルパー ---
        private GameObject FindGameObjectIncludingInactive(string name)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null) return obj;

            Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (Transform t in allTransforms)
            {
                if (t.gameObject.name.Equals(name, StringComparison.OrdinalIgnoreCase) && t.gameObject.hideFlags == HideFlags.None)
                {
                    return t.gameObject;
                }
            }
            return null;
        }
    }
}
