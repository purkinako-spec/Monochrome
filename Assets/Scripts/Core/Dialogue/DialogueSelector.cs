using System;
using System.Collections.Generic;
using UnityEngine;
using InteractionSystem2D;
using Monochrome.Core.Input;
using Runtime.Dialogue;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

namespace Monochrome.Core.Dialogue
{
    [Serializable]
    public class ConditionalDialogueEntry
    {
        [Tooltip("この会話が発生するためのフラグ条件（複数指定時は AND 判定）")]
        public List<FlagCondition> conditions = new List<FlagCondition>();

        [Tooltip("再生する会話コンテナ名（Addressables アドレスまたはアセット名）")]
        public string dialogueContainerName = "";

        [Tooltip("インスペクタで直接コンテナをアサインする場合（開発・テスト用フォールバック）")]
        public DialogueContainer directContainerAsset = null;
    }

    /// <summary>
    /// NPC の会話進行度・条件分岐を統括するセレクターコンポーネント。
    /// - FlagManager の条件（クエスト受託中、アイテム所持、会話回数など）を優先度順に自動判定
    /// - 初回会話（talk_count == 0）、クエスト会話、汎用会話を動的に切り替え
    /// - 会話終了時に自動でトークカウントフラグ（talk_count_{npcId}）を加算
    /// - 既存の IInteractable2D を実装し、PlayerInteractor2D から [E] / (A) で直接起動
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Monochrome/Dialogue/Dialogue Selector")]
    public class DialogueSelector : MonoBehaviour, IInteractable2D
    {
        [Header("NPC Identity")]
        [Tooltip("NPC の識別ID（フラグ管理用。例: 'npc_elder', 'npc_fisherman'）")]
        [SerializeField] private string npcId = "npc_default";

        [Header("Dialogue Branch List (上から順に優先評価)")]
        [SerializeField] private List<ConditionalDialogueEntry> dialogueBranches = new List<ConditionalDialogueEntry>();

        [Header("Default Fallback Dialogue")]
        [Tooltip("どの条件にも当てはまらなかった場合のデフォルト会話コンテナ名")]
        [SerializeField] private string defaultContainerName = "";
        [SerializeField] private DialogueContainer defaultDirectContainer = null;

        [Header("Interaction Settings")]
        [SerializeField] private string requiredKey = "Interact";
        [SerializeField] private string talkActionDescription = "話す";

        [Header("Auto Increment Settings")]
        [Tooltip("会話終了時に自動で talk_count_{npcId} を +1 加算するか")]
        [SerializeField] private bool autoIncrementTalkCount = true;

        
        /// <summary>
        /// スクリプト等から直接会話コンテナを割り当てる（テスト・動的割り当て用）
        /// </summary>
        public void SetDefaultDirectContainer(DialogueContainer container)
        {
            defaultDirectContainer = container;
        }

        public string RequiredKey => requiredKey;
        public string NpcId => npcId;

        // トークカウント取得プロパティ
        public int TalkCount => FlagManager.Instance != null ? FlagManager.Instance.GetFlag($"talk_count_{npcId}") : 0;

        public bool TryInteract(string key)
        {
            if (key != requiredKey) return false;

            StartSelectedDialogue();
            return true;
        }

        public string GetInteractPrompt()
        {
            return InputPromptResolver.GetPromptText(requiredKey, talkActionDescription);
        }

        /// <summary>
        /// 条件に合致する会話を選択して再生します
        /// </summary>
        public void StartSelectedDialogue()
        {
            var targetContainer = ResolveContainer();
            if (targetContainer == null)
            {
                Debug.LogWarning($"[DialogueSelector] NPC '{npcId}' に再生可能な会話コンテナが見つかりません。", this);
                return;
            }

            // 会話相手（自身）の自動停止＆振り向き
            var nav = GetComponent<Character.CharacterNavigator2D>();
            if (nav != null && Character.PlayerController2D.LocalPlayer != null)
            {
                nav.PauseAndFace(Character.PlayerController2D.LocalPlayer.transform.position);
            }

            // プレイヤーを相手の方向へ向かせる
            if (Character.PlayerController2D.LocalPlayer != null)
            {
                Character.PlayerController2D.LocalPlayer.Motor.SetFacingDirection(
                    transform.position - Character.PlayerController2D.LocalPlayer.transform.position);
                Character.PlayerController2D.LocalPlayer.BlockState.AddBlocker(ActionBlocker.Dialogue);
            }

            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue(targetContainer, gameObject, OnDialogueFinished);
            }
        }

        private void OnDialogueFinished()
        {
            // トークカウント自動加算
            if (autoIncrementTalkCount && FlagManager.Instance != null)
            {
                FlagManager.Instance.AddFlag($"talk_count_{npcId}", 1);
            }

            // NPC のナビゲーション再開
            var nav = GetComponent<Character.CharacterNavigator2D>();
            if (nav != null)
            {
                nav.ResumeNavigation();
            }

            // プレイヤー操作ロック解除
            if (Character.PlayerController2D.LocalPlayer != null)
            {
                Character.PlayerController2D.LocalPlayer.BlockState.RemoveBlocker(ActionBlocker.Dialogue);
            }

            Debug.Log($"[DialogueSelector] NPC '{npcId}' の会話が終了しました (TalkCount: {TalkCount})");
        }

        private DialogueContainer ResolveContainer()
        {
            var flags = FlagManager.Instance;

            // 1. 条件分岐リストを上から順に判定
            if (dialogueBranches != null && flags != null)
            {
                foreach (var branch in dialogueBranches)
                {
                    if (branch.conditions == null || branch.conditions.Count == 0 || flags.EvaluateAll(branch.conditions))
                    {
                        var c = LoadOrGetContainer(branch.dialogueContainerName, branch.directContainerAsset);
                        if (c != null) return c;
                    }
                }
            }

            // 2. フォールバック
            return LoadOrGetContainer(defaultContainerName, defaultDirectContainer);
        }

        private DialogueContainer LoadOrGetContainer(string address, DialogueContainer directAsset)
        {
            // 直接アサインがあればそれを優先
            if (directAsset != null) return directAsset;

            // Addressables キャッシュから取得
            if (!string.IsNullOrEmpty(address) && DialogueContainerManager.Instance != null)
            {
                return DialogueContainerManager.Instance.GetLoaded(address);
            }
            return null;
        }
    }
}
