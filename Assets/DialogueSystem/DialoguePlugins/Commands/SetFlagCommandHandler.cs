using System;
using UnityEngine;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

namespace Runtime.Dialogue.Plugins.Commands
{
    [HandlerInfo(
    description: @"ゲーム内のフラグや変数（FlagManager）の値を設定・更新するためのコマンドハンドラーです[cite: 6]。",
    usage: @"【基本パラメータ】
[set_flag:key=変数名, val=数値]

【使用例】
・フラグを1に設定: [set_flag:key=has_key,val=1][cite: 6]
・フラグを0にリセット: [set_flag:key=quest_clear,val=0]"
)]
    public class SetFlagCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "set_flag";

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
                DialogueEventDispatcher.Instance.RegisterHandler(this);
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            string key = command.GetString("key", "");
            int val = command.GetInt("val", 1);

            if (!string.IsNullOrEmpty(key) && FlagManager.Instance != null)
            {
                FlagManager.Instance.SetFlag(key, val);
                Debug.Log($"[FlagManager] フラグ更新: {key} = {val}");
            }

            onComplete?.Invoke(); // 一瞬で終わるので即座に次へ
        }

        public void ForceComplete(DialogueCommand command)
        {
            // スキップ時も確実にフラグはセットする必要がある
            Execute(command, null);
        }
    }
}