using System;
using System.Collections.Generic;
using UnityEngine;
using Runtime.Dialogue.Core; // IDialogueCommandHandler がある名前空間を指定
// using Runtime.Dialogue.Commands; // ※環境によってはこちらも必要

namespace Runtime.Dialogue
{
    /// <summary>
    /// 各コマンドの実行を、登録されたハンドラーに丸投げするディスパッチャ
    /// </summary>
    public class DialogueEventDispatcher : MonoBehaviour
    {
        public static DialogueEventDispatcher Instance { get; private set; }

        // コマンド名と、それを処理するクラス(ハンドラー)の辞書
        private Dictionary<string, IDialogueCommandHandler> handlers = new Dictionary<string, IDialogueCommandHandler>();

        private void Awake()
        {
            if (Instance == null) { Instance = this; }
            else { Destroy(gameObject); }
        }

        /// <summary>
        /// 外部のスクリプトから新しい演出コマンドをシステムに登録する
        /// </summary>
        public void RegisterHandler(IDialogueCommandHandler handler)
        {
            if (handler == null || string.IsNullOrEmpty(handler.TargetCommandName)) return;

            string key = handler.TargetCommandName.ToLower();
            if (!handlers.ContainsKey(key))
            {
                handlers.Add(key, handler);
            }
        }

        public void ExecuteCommand(DialogueCommand command, Action onComplete)
        {
            if (command == null)
            {
                onComplete?.Invoke();
                return;
            }

            // 【修正】C#の正しいNullチェック構文
            string key = command.CommandName?.ToLower() ?? string.Empty;

            if (handlers.TryGetValue(key, out var handler))
            {
                handler.Execute(command, () =>
                {
                    command.IsExecuted = true;
                    onComplete?.Invoke();
                });
            }
            else
            {
                Debug.LogWarning($"[Dialogue] 未登録のコマンド '{key}' が来ました。");
                command.IsExecuted = true;
                onComplete?.Invoke();
            }
        }

        public void ForceCompleteCommand(DialogueCommand command)
        {
            if (command == null || command.IsExecuted) return;
            command.IsExecuted = true;

            // 【修正】C#の正しいNullチェック構文
            string key = command.CommandName?.ToLower() ?? string.Empty;

            if (handlers.TryGetValue(key, out var handler))
            {
                handler.ForceComplete(command);
            }
        }
    }
}