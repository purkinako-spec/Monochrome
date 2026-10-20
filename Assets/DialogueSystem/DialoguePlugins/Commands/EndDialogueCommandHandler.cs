using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;
using System;
using System.Collections;
using UnityEngine;

namespace Runtime.Dialogue.Commands
{
    /// <summary>
    /// [end_dialogue:fade=0.5] コマンドを処理するプラグイン
    /// ダイアログUI（CanvasGroup）をフェードアウトさせ、その後安全に会話を終了します。
    /// </summary>
    [HandlerInfo(description: "ダイアログUIをフェードアウトさせた後、安全に会話を終了します。", usage: "[end_dialogue:fade=0.5]")]
    public class EndDialogueCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "end_dialogue";

        [Tooltip("フェードアウトさせたいダイアログUI全体の CanvasGroup をセットしてください")]
        [SerializeField] private CanvasGroup dialogueCanvasGroup;

        private bool isFading = false;

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            float duration = command.GetFloat("fade", 0.5f);
            StartCoroutine(EndSequenceRoutine(duration, onComplete));
        }

        public void ForceComplete(DialogueCommand command)
        {
            // フェード処理中の強制終了（スキップ連打等）をブロック
            if (isFading) return;

            StopAllCoroutines();

            if (dialogueCanvasGroup != null)
            {
                dialogueCanvasGroup.alpha = 0f;
                dialogueCanvasGroup.blocksRaycasts = false;
            }

            CloseDialogueDirectly();
        }

        private IEnumerator EndSequenceRoutine(float duration, Action onComplete)
        {
            isFading = true;

            // 1. 演出中にボタン連打で進まないよう外部入力をロック
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.SetExternalInputLock(true);
            }

            // 2. ダイアログUIの不透明度（Alpha）を徐々に 0 へ落とす
            if (dialogueCanvasGroup != null)
            {
                float startAlpha = dialogueCanvasGroup.alpha;
                float timer = 0f;

                while (timer < duration)
                {
                    timer += Time.deltaTime;
                    dialogueCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(timer / duration));
                    yield return null;
                }

                dialogueCanvasGroup.alpha = 0f;
                dialogueCanvasGroup.blocksRaycasts = false;
            }
            else
            {
                Debug.LogWarning("[EndDialogueCommandHandler] dialogueCanvasGroup が設定されていません！");
            }

            isFading = false;

            // 3. UIが完全に消えたら会話を終了する
            CloseDialogueDirectly();

            // 4. システムへ完了を通知
            onComplete?.Invoke();
        }

        private void CloseDialogueDirectly()
        {
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.SetExternalInputLock(false);
                DialogueManager.Instance.EndDialogue();
            }
        }
    }
}
