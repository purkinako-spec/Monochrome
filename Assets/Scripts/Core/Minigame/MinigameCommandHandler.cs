using System;
using UnityEngine;
using Runtime.Dialogue;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

namespace Monochrome.Core.Minigame
{
    /// <summary>
    /// 会話ノード内からミニゲーム（釣り等）を直接起動するダイアログコマンドハンドラー。
    /// 書式: [minigame:scene=FishingMinigameSceneTest1, flag=minigame_fishing_cleared, result=FishingResultDialogue, fade=true, closeDialogue=true]
    /// </summary>
    [HandlerInfo(
        description: "会話ノード内から加算（Additive）ミニゲームシーンを安全に起動します。フィールド停止・暗転・結果フラグ更新・終了後会話を一括制御します。",
        usage: "[minigame:scene=FishingMinigameSceneTest1, flag=minigame_fishing_cleared, result=ResultDialogueContainer, fade=true]"
    )]
    [AddComponentMenu("Monochrome/Minigame/Minigame Command Handler")]
    public class MinigameCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "minigame";

        private void Awake()
        {
            TryRegister();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void TryRegister()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            string sceneName = command.GetString("scene", "FishingMinigameSceneTest1");
            string clearFlagKey = command.GetString("flag", "minigame_fishing_cleared");
            string resultDialogue = command.GetString("result", "");
            bool useFade = command.GetBool("fade", true);
            bool closeDialogue = command.GetBool("closeDialogue", true);

            Debug.Log($"[MinigameCommandHandler] コマンド受信: scene='{sceneName}', flag='{clearFlagKey}', fade={useFade}");

            // 会話を閉じてからミニゲームへ突入する場合
            if (closeDialogue && DialogueManager.Instance != null)
            {
                DialogueManager.Instance.EndDialogue();
            }

            var request = new MinigameLaunchRequest
            {
                minigameSceneName       = sceneName,
                clearFlagKey            = clearFlagKey,
                resultDialogueContainer = resultDialogue,
                useFade                 = useFade,
                onComplete              = onComplete
            };

            MinigameConnector.Instance.LaunchMinigame(request);
        }

        public void ForceComplete(DialogueCommand command)
        {
            Execute(command, null);
        }
    }
}
