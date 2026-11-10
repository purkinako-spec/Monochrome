using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Monochrome.Core.Transition
{
    /// <summary>
    /// 全画面フェードイン・フェードアウトを提供する常駐キャンバスUI。
    /// シーンを跨いでも破棄されず、エリア遷移やカットシーンの暗転演出を行います。
    /// </summary>
    [AddComponentMenu("Monochrome/Transition/Screen Fader")]
    public class ScreenFader : MonoBehaviour
    {
        private static ScreenFader instance;

        public static ScreenFader Instance
        {
            get
            {
                if (instance == null || !instance)
                {
                    instance = FindFirstObjectByType<ScreenFader>();
                    if ((instance == null || !instance) && Application.isPlaying)
                    {
                        var go = new GameObject("[ScreenFader]");
                        instance = go.AddComponent<ScreenFader>();
                        instance.BuildCanvasUI();
                        if (go.transform.parent == null) DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [SerializeField] private Image fadeImage;
        [SerializeField] private Canvas canvas;

        private Coroutine activeFadeRoutine;
        private bool isFadedOut = false;

        public bool IsFadedOut => isFadedOut;

        private void Awake()
        {
            if (instance == null || !instance)
            {
                instance = this;
                if (transform.parent == null) DontDestroyOnLoad(gameObject);
                if (fadeImage == null) BuildCanvasUI();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// フェード用のUI CanvasとフルスクリーンImageをコードから自動生成します
        /// </summary>
        private void BuildCanvasUI()
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // 最前面に配置

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = gameObject.AddComponent<GraphicRaycaster>();

            // フェード用 Image
            var imgObj = new GameObject("FadeOverlay");
            imgObj.transform.SetParent(transform, false);
            var rect = imgObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            fadeImage = imgObj.AddComponent<Image>();
            fadeImage.color = new Color(0, 0, 0, 0);
            fadeImage.raycastTarget = false; // 透明時はクリックを邪魔しない
        }

        /// <summary>
        /// 画面を暗転（フェードアウト）させます
        /// </summary>
        public async Task FadeOutAsync(float duration = 0.5f, Color? fadeColor = null)
        {
            if (fadeImage == null) BuildCanvasUI();

            Color targetColor = fadeColor ?? Color.black;
            targetColor.a = 1f;

            Color startColor = fadeImage.color;
            startColor.r = targetColor.r;
            startColor.g = targetColor.g;
            startColor.b = targetColor.b;
            fadeImage.color = startColor;

            if (activeFadeRoutine != null) StopCoroutine(activeFadeRoutine);

            var tcs = new TaskCompletionSource<bool>();
            activeFadeRoutine = StartCoroutine(FadeRoutine(0f, 1f, duration, tcs));
            await tcs.Task;

            isFadedOut = true;
        }

        /// <summary>
        /// 画面の暗転を解除（フェードイン）させます
        /// </summary>
        public async Task FadeInAsync(float duration = 0.5f)
        {
            if (fadeImage == null) return;

            if (activeFadeRoutine != null) StopCoroutine(activeFadeRoutine);

            var tcs = new TaskCompletionSource<bool>();
            activeFadeRoutine = StartCoroutine(FadeRoutine(1f, 0f, duration, tcs));
            await tcs.Task;

            isFadedOut = false;
        }

        private IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration, TaskCompletionSource<bool> tcs)
        {
            fadeImage.raycastTarget = toAlpha > 0.01f; // 暗転中は入力をブロック
            float elapsed = 0f;

            Color c = fadeImage.color;
            c.a = fromAlpha;
            fadeImage.color = c;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime; // TimeScaleの影響を受けずにフェード
                float t = Mathf.Clamp01(elapsed / duration);
                c.a = Mathf.Lerp(fromAlpha, toAlpha, t);
                fadeImage.color = c;
                yield return null;
            }

            c.a = toAlpha;
            fadeImage.color = c;
            fadeImage.raycastTarget = toAlpha > 0.01f;
            activeFadeRoutine = null;
            tcs.TrySetResult(true);
        }
    }
}
