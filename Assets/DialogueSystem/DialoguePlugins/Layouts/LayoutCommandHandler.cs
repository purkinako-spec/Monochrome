using System;
using UnityEngine;
using Runtime.Dialogue;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

[HandlerInfo(
    description: @"ダイアログウィンドウのレイアウトや枠の見た目、表示スタイルを切り替えるコマンドハンドラーです。",
    usage: @"【基本パラメータ】
[layout:name=レイアウト名]

【使用例】
・標準レイアウト: [layout:name=Normal]
・ナレーション用: [layout:name=Narration]
・全画面表示用: [layout:name=FullScreen]"
)]
public class LayoutCommandHandler : MonoBehaviour, IDialogueCommandHandler
{
    public string TargetCommandName => "layout";

    private void Start()
    {
        if (DialogueEventDispatcher.Instance != null)
        {
            DialogueEventDispatcher.Instance.RegisterHandler(this);
        }
    }

    public void Execute(DialogueCommand command, Action onComplete)
    {
        string layoutName = command.GetString("name", "Normal");

        if (DialogueLayoutDispatcher.Instance != null)
        {
            DialogueLayoutDispatcher.Instance.TryHandleLayout(layoutName, command.Arguments);
        }

        onComplete?.Invoke();
    }

    public void ForceComplete(DialogueCommand command)
    {
        StopAllCoroutines();

        string layoutName = command.GetString("name", "Normal");

        if (DialogueLayoutDispatcher.Instance != null)
        {
            DialogueLayoutDispatcher.Instance.TryHandleLayout(layoutName, command.Arguments);
        }
    }
}