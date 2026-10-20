using Runtime.Dialogue.Core;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI; // 👈 追加

namespace Runtime.Dialogue.Commands
{
    /// <summary>
    /// [fade_in] コマンドを処理するプラグイン
    /// </summary>
    [HandlerInfo(description: "画面を暗転状態から徐々に明るくするフェードイン演出を行います。", usage: "[fade_in:time=1.0]")]
    public class FadeInCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "fade_in";

        [Tooltip("明転用の黒い画像（画面全体を覆うUI）をセットしてください（FadeOutと同じものでOKです）")]
        [SerializeField] private Image fadePanel;

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }
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
                // 一瞬で完全に透明（明転）にして非表示にする
                Color c = fadePanel.color;
                c.a = 0f;
                fadePanel.color = c;
                fadePanel.gameObject.SetActive(false);
            }
            Debug.Log("[FadeIn] スキップ：一瞬で明転させました");
        }

        private IEnumerator FadeRoutine(float duration, Action onComplete)
        {
            if (fadePanel == null)
            {
                Debug.LogWarning("[FadeIn] fadePanelが設定されていません！");
                onComplete?.Invoke();
                yield break;
            }

            fadePanel.gameObject.SetActive(true);
            Color color = fadePanel.color;

            // フェードイン開始時は真っ暗（Alpha = 1）の状態からスタート
            color.a = 1f;
            fadePanel.color = color;

            float timer = 0f;

            // duration秒かけてAlphaを1から0へ減少させる
            while (timer < duration)
            {
                timer += Time.deltaTime;
                color.a = Mathf.Clamp01(1f - (timer / duration));
                fadePanel.color = color;
                yield return null; // 1フレーム待機
            }

            color.a = 0f;
            fadePanel.color = color;
            fadePanel.gameObject.SetActive(false); // 完全に透明になったら非表示に

            onComplete?.Invoke();
        }
    }
}



