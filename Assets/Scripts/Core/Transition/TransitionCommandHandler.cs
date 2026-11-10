using System;
using UnityEngine;
using Runtime.Dialogue;
using Runtime.Dialogue.Core;

namespace Monochrome.Core.Transition
{
    /// <summary>
    /// 会話ノード内からエリア遷移（シーン移動またはシーン内ワープ）を直接発火させるコマンドハンドラー。
    /// 書式: [transition:scene=ForestScene, spawn=Entrance, style=FadeOnly, resume=Forest_Event]
    /// </summary>
    [AddComponentMenu("Monochrome/Cutscene/Transition Command Handler")]
    public class TransitionCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "transition";

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            string sceneName = command.GetString("scene", "");
            string spawnId = command.GetString("spawn", "Default");
            string styleStr = command.GetString("style", "FadeOnly");
            string resumeDialogue = command.GetString("resume", "");
            float fadeDuration = command.GetFloat("duration", 0.4f);

            Enum.TryParse<TransitionStyle>(styleStr, true, out var style);

            var request = new TransitionRequest
            {
                destinationScene = sceneName,
                spawnPointId     = spawnId,
                style            = style,
                fadeDuration     = fadeDuration,
                resumeDialogueContainer = resumeDialogue,
                onComplete       = onComplete
            };

            AreaTransitionManager.Instance.Transition(request);
        }

        public void ForceComplete(DialogueCommand command) => Execute(command, null);
    }
}
