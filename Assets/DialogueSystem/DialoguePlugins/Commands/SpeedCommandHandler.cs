using Runtime.Dialogue;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;
using System;
using UnityEngine;

namespace Runtime.Dialogue.Commands
{
    [HandlerInfo("文字表示速度の変更", "使い方: [speed:val=0.02] (数値が小さいほど高速)")]
    public class SpeedCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "speed";

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
                DialogueEventDispatcher.Instance.RegisterHandler(this);
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            float speed = command.GetFloat("val", command.GetFloat("default", 0.05f));

            var view = DialogueManager.Instance.CurrentView as DialogueViewWindow;
            if (view != null)
            {
                view.SetTypingSpeed(speed);
            }

            onComplete?.Invoke();
        }

        public void ForceComplete(DialogueCommand command) { }
    }
}