using Runtime.Dialogue.Core;
using Runtime.Dialogue.Audio;
using System;
using UnityEngine;

namespace Runtime.Dialogue.Commands
{
    /// <summary>
    /// [se:name=SEの名前] または [se:clip=SEの名前] を処理するコマンドハンドラー
    /// </summary>
    [HandlerInfo(description: "指定された名前のSE（効果音）をデータベースから再生します。", usage: "[se:name=SEの名前]")]
    public class SECommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "se";

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            PlaySE(command);

            // SEは鳴らしっぱなしで会話を進行させるため即時完了
            onComplete?.Invoke();
        }

        public void ForceComplete(DialogueCommand command)
        {
            // スキップ時も効果音を出力（連打対策等はAudioManager側またはここで行う）
            PlaySE(command);
        }

        private void PlaySE(DialogueCommand command)
        {
            string seKey = command.GetString("name", command.GetString("clip", ""));

            if (!string.IsNullOrEmpty(seKey))
            {
                DialogueAudioManager.Instance?.PlaySE(seKey);
            }
        }
    }
}