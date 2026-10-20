using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;
using System;
using UnityEngine;

namespace Runtime.Dialogue.Commands
{
    /// <summary>
    /// [advance] コマンドを処理するプラグイン
    /// </summary>
    [HandlerInfo(description: "会話を次のノードへ強制的に進行させます。", usage: "[advance]")]
    public class AdvanceCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "advance";

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            Advance();
            onComplete?.Invoke();
        }

        public void ForceComplete(DialogueCommand command)
        {
            Advance();
        }

        private void Advance()
        {
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.SetExternalInputLock(false);
                DialogueManager.Instance.HandleAdvanceInput();
            }
        }
    }
}
