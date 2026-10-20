using Runtime.Dialogue.Core;
using System;
using System.Collections;
using UnityEngine;

namespace Runtime.Dialogue.Commands
{
    /// <summary>
    /// [wait:time=秒数] 指定した秒数だけ文字送りを一時停止させるプラグイン
    /// </summary>
    [HandlerInfo(description: "指定した秒数だけ会話の進行（文字送り）を一時停止します。", usage: "[wait:time=1.0]")]
    public class WaitCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "wait";

        private Coroutine currentWaitRoutine;
        private Action currentOnComplete;

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
                DialogueEventDispatcher.Instance.RegisterHandler(this);
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            float duration = command.GetFloat("time", 1.0f);
            
            // 既に動いている待機があれば停止してコールバックを消化
            StopCurrentWait();

            currentOnComplete = onComplete;
            currentWaitRoutine = StartCoroutine(WaitRoutine(duration));
        }

        public void ForceComplete(DialogueCommand command)
        {
            // スキップされたら待機を即座にやめ、未完了だったコールバックを強制実行する
            if (currentWaitRoutine != null)
            {
                StopCoroutine(currentWaitRoutine);
                currentWaitRoutine = null;
            }

            var tempCallback = currentOnComplete;
            currentOnComplete = null;
            tempCallback?.Invoke();
        }

        private IEnumerator WaitRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            
            currentWaitRoutine = null;
            var tempCallback = currentOnComplete;
            currentOnComplete = null;
            tempCallback?.Invoke(); // 正常終了時にコールバックを呼ぶ
        }

        private void StopCurrentWait()
        {
            if (currentWaitRoutine != null)
            {
                StopCoroutine(currentWaitRoutine);
                currentWaitRoutine = null;
            }
            currentOnComplete = null;
        }
    }
}