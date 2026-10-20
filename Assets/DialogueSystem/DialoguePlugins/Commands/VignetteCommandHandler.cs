using Runtime.Dialogue.Core;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Runtime.Dialogue.Commands
{
    /// <summary>
    /// [vignette:state=T/F,time=0.5,intensity=0.8] を処理するコマンドハンドラー
    /// </summary>
    [HandlerInfo(description: "画面効果・演出（ビネット効果）を制御し、アニメーションを実行します。", usage: "[vignette:state=T,time=0.5,intensity=0.8]")]
    public class VignetteCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "vignette";

        [Tooltip("制御対象の Volume。未設定時はシーン内から自動取得します")]
        [SerializeField] private Volume postProcessVolume;

        private Vignette vignette;

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }

            InitVignette();
        }

        private void InitVignette()
        {
            if (postProcessVolume == null)
            {
                postProcessVolume = FindFirstObjectByType<Volume>();
            }

            if (postProcessVolume != null && postProcessVolume.profile != null)
            {
                postProcessVolume.profile.TryGet(out vignette);
            }
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            if (vignette == null) InitVignette();

            if (vignette == null)
            {
                Debug.LogWarning("[VignetteCommandHandler] Vignette を持つ Volume が見つかりませんでした。");
                onComplete?.Invoke();
                return;
            }

            string stateStr = command.GetString("state", "T");
            bool isEnable = stateStr.Equals("T", StringComparison.OrdinalIgnoreCase) ||
                            stateStr.Equals("True", StringComparison.OrdinalIgnoreCase);

            float duration = command.GetFloat("time", command.GetFloat("duration", 0.5f));
            float targetIntensity = command.GetFloat("intensity", 0.8f);

            StopAllCoroutines();
            StartCoroutine(VignetteRoutine(isEnable, targetIntensity, duration, onComplete));
        }

        public void ForceComplete(DialogueCommand command)
        {
            StopAllCoroutines();
            if (vignette == null) InitVignette();

            if (vignette != null)
            {
                string stateStr = command.GetString("state", "T");
                bool isEnable = stateStr.Equals("T", StringComparison.OrdinalIgnoreCase) ||
                                stateStr.Equals("True", StringComparison.OrdinalIgnoreCase);

                float targetIntensity = command.GetFloat("intensity", 0.8f);

                vignette.active = true;
                vignette.intensity.value = isEnable ? targetIntensity : 0f;
            }
        }

        private IEnumerator VignetteRoutine(bool isEnable, float maxIntensity, float duration, Action onComplete)
        {
            vignette.active = true;

            float startValue = vignette.intensity.value;
            float endValue = isEnable ? maxIntensity : 0f;

            if (duration <= 0f)
            {
                vignette.intensity.value = endValue;
                onComplete?.Invoke();
                yield break;
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                vignette.intensity.value = Mathf.Lerp(startValue, endValue, timer / duration);
                yield return null;
            }

            vignette.intensity.value = endValue;
            onComplete?.Invoke();
        }
    }
}
