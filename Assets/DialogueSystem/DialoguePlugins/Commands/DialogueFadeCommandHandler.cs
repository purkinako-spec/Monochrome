using System;
using System.Collections;
using UnityEngine;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Plugins.Commands
{
    /// <summary>
    /// 会話ウィンドウ（ダイアログ枠）全体のフェードイン/フェードアウト演出を行うプラグイン
    /// </summary>
    [HandlerInfo(
        description: @"ダイアログウィンドウ（CanvasGroup）全体のフェードイン・フェードアウト演出を行うコマンドハンドラーです。
会話の開始・終了時の自動フェードイン・アウトにも対応しています。",
        usage: @"【基本パラメータ】
[dialogue_fade:type=in/out, time=秒数]

【使用例】
・フェードイン表示 (0.5秒): [dialogue_fade:type=in,time=0.5]
・フェードアウト非表示 (1.0秒): [dialogue_fade:type=out,time=1.0]"
    )]
    public class DialogueFadeCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "dialogue_fade";

        [Header("Target UI")]
        [Tooltip("フェードさせるダイアログUI全体の CanvasGroup をセットしてください")]
        [SerializeField] private CanvasGroup targetCanvasGroup;

        [Header("Auto Fade Settings")]
        [Tooltip("会話表示開始時に自動でフェードインを行うか")]
        [SerializeField] private bool autoFadeInOnStart = true;
        [SerializeField] private float autoFadeInTime = 0.5f;

        [Space(5)]
        [Tooltip("会話終了時に自動でフェードアウトを行うか")]
        [SerializeField] private bool autoFadeOutOnEnd = true;
        [SerializeField] private float autoFadeOutTime = 0.5f;

        private Coroutine currentFadeCoroutine;

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }

            // 初期状態の透明化設定
            if (autoFadeInOnStart && targetCanvasGroup != null)
            {
                targetCanvasGroup.alpha = 0f;
                targetCanvasGroup.blocksRaycasts = false;
            }
        }

        // --- 会話開始時（UI表示時）に呼ぶパブリックメソッド ---
        public void OnDialogueStart()
        {
            if (!autoFadeInOnStart || targetCanvasGroup == null) return;
            StartFade("in", autoFadeInTime, null);
        }

        // --- 会話終了時（UI非表示時）に呼ぶパブリックメソッド ---
        public void OnDialogueEnd()
        {
            if (!autoFadeOutOnEnd || targetCanvasGroup == null) return;
            StartFade("out", autoFadeOutTime, null);
        }

        // --- コマンド実行ハンドラー ---
        public void Execute(DialogueCommand command, Action onComplete)
        {
            if (targetCanvasGroup == null)
            {
                Debug.LogWarning("[DialogueFade] targetCanvasGroup が設定されていません！");
                onComplete?.Invoke();
                return;
            }

            string type = command.GetString("type", "in").ToLower();
            float duration = command.GetFloat("time", 0.5f);

            StartFade(type, duration, onComplete);
        }

        public void ForceComplete(DialogueCommand command)
        {
            if (currentFadeCoroutine != null)
            {
                StopCoroutine(currentFadeCoroutine);
            }

            if (targetCanvasGroup != null)
            {
                string type = command.GetString("type", "in").ToLower();
                float targetAlpha = (type == "in") ? 1f : 0f;

                targetCanvasGroup.alpha = targetAlpha;
                targetCanvasGroup.blocksRaycasts = (targetAlpha > 0f);
            }
        }

        private void StartFade(string type, float duration, Action onComplete)
        {
            float targetAlpha = (type == "in") ? 1f : 0f;

            if (currentFadeCoroutine != null)
            {
                StopCoroutine(currentFadeCoroutine);
            }

            currentFadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, duration, onComplete));
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration, Action onComplete)
        {
            float startAlpha = targetCanvasGroup.alpha;
            float timer = 0f;

            if (targetAlpha > 0f)
            {
                targetCanvasGroup.blocksRaycasts = true;
            }

            while (timer < duration)
            {
                timer += Time.deltaTime;
                targetCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(timer / duration));
                yield return null;
            }

            targetCanvasGroup.alpha = targetAlpha;

            if (Mathf.Approximately(targetAlpha, 0f))
            {
                targetCanvasGroup.blocksRaycasts = false;
            }

            currentFadeCoroutine = null;
            onComplete?.Invoke();
        }
    }
}