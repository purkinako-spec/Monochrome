using Runtime.Dialogue.Core;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI; // 👈 追加

namespace Runtime.Dialogue.Commands
{
    /// <summary>
    /// [fade_out] コマンドを処理するプラグイン
    /// </summary>
    [HandlerInfo(description: "画面を徐々に暗転させるフェードアウト演出を行います。", usage: "[fade_out:time=1.0]")]
    public class FadeOutCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "fade_out";

        [Tooltip("暗転用の黒い画像（画面全体を覆うUI）をセットしてください")]
        [SerializeField] private Image fadePanel;

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
                DialogueEventDispatcher.Instance.RegisterHandler(this);

            // 最初は透明にしておく
            /*
            if (fadePanel != null)
            {
                Color c = fadePanel.color;
                c.a = 0f;
                fadePanel.color = c;
                fadePanel.gameObject.SetActive(false);
            }
            */
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            float duration = command.GetFloat("time", 1.0f);
            StartCoroutine(FadeRoutine(duration, onComplete));
        }

        public void ForceComplete(DialogueCommand command)
        {
            StopAllCoroutines();
            if (fadePanel != null)
            {
                // 一瞬で真っ黒にする
                Color c = fadePanel.color;
                c.a = 1f;
                fadePanel.color = c;
                fadePanel.gameObject.SetActive(true);
            }
            Debug.Log("[FadeOut] スキップ：一瞬で真っ黒にしました");
        }

        private IEnumerator FadeRoutine(float duration, Action onComplete)
        {
            if (fadePanel == null)
            {
                Debug.LogWarning("[FadeOut] fadePanelが設定されていません！");
                onComplete?.Invoke();
                yield break;
            }

            // 表示前に alpha = 0f (完全透明) に設定して徐々に暗転させる
            Color color = fadePanel.color;
            color.a = 0f;
            fadePanel.color = color;

            fadePanel.gameObject.SetActive(true);

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                color.a = Mathf.Clamp01(timer / duration);
                fadePanel.color = color;
                yield return null;
            }

            color.a = 1f;
            fadePanel.color = color;
            onComplete?.Invoke();
        }
    }
}