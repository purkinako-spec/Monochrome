using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Monochrome.Core.Transition
{
    /// <summary>
    /// シーン間をまたぐ大規模ロード時にプログレスバーと演出を表示するローディングUI。
    /// AsyncOperation.allowSceneActivation = false を活用し、裏読み込みが100%完了するまで待機してから
    /// スムーズに画面を切り替えることで、ロード中の描画フリーズ（カクつき）を完全に防止します。
    /// </summary>
    [AddComponentMenu("Monochrome/Transition/Loading Screen UI")]
    public class LoadingScreenUI : MonoBehaviour
    {
        private static LoadingScreenUI instance;

        public static LoadingScreenUI Instance
        {
            get
            {
                if (instance == null || !instance)
                {
                    instance = FindFirstObjectByType<LoadingScreenUI>();
                    if ((instance == null || !instance) && Application.isPlaying)
                    {
                        var go = new GameObject("[LoadingScreenUI]");
                        instance = go.AddComponent<LoadingScreenUI>();
                        instance.BuildUI();
                        if (go.transform.parent == null) DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [SerializeField] private GameObject rootPanel;
        [SerializeField] private Slider progressBar;
        [SerializeField] private Text progressText;

        private void Awake()
        {
            if (instance == null || !instance)
            {
                instance = this;
                if (transform.parent == null) DontDestroyOnLoad(gameObject);
                if (rootPanel == null) BuildUI();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void BuildUI()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 998; // ScreenFader(999)の直下に配置

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = gameObject.AddComponent<GraphicRaycaster>();

            // パネル本体
            rootPanel = new GameObject("LoadingPanel");
            rootPanel.transform.SetParent(transform, false);
            var rect = rootPanel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            var bg = rootPanel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.08f, 1f); // 落ち着いたダークグレー

            // プログレスバー背景
            var barBgObj = new GameObject("ProgressBar");
            barBgObj.transform.SetParent(rootPanel.transform, false);
            var barRect = barBgObj.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.2f, 0.15f);
            barRect.anchorMax = new Vector2(0.8f, 0.18f);
            barRect.sizeDelta = Vector2.zero;

            progressBar = barBgObj.AddComponent<Slider>();
            progressBar.interactable = false;
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;

            var bgImg = barBgObj.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // 塗りつぶし領域 (Fill)
            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(barBgObj.transform, false);
            var fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            var fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            progressBar.fillRect = fillRect;

            rootPanel.SetActive(false);
        }

        /// <summary>
        /// 指定したシーンを非同期ロードし、進行状況を表示しながら完了まで管理します。
        /// </summary>
        public async Task LoadSceneAsyncWithProgress(string sceneName, float minDisplayTime = 0.5f)
        {
            if (rootPanel == null) BuildUI();

            rootPanel.SetActive(true);
            if (progressBar != null) progressBar.value = 0f;

            // 1. メモリクリーンアップ（暗転中の裏で実行してカクつき防止）
            await Resources.UnloadUnusedAssets();
            GC.Collect();

            // 2. 非同期シーンロード開始 (allowSceneActivation = false で裏読み込み)
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            op.allowSceneActivation = false;

            float startTime = Time.unscaledTime;

            // 3. 0.0 〜 0.9 の進捗を 0.0 〜 1.0 にマッピングしてプログレスバーを更新
            while (op.progress < 0.9f || (Time.unscaledTime - startTime) < minDisplayTime)
            {
                float normalizedProgress = Mathf.Clamp01(op.progress / 0.9f);
                if (progressBar != null) progressBar.value = normalizedProgress;
                await Task.Yield();
            }

            if (progressBar != null) progressBar.value = 1f;

            // 4. ロード完了を許可してシーンをアクティブ化
            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                await Task.Yield();
            }

            // 5. ロード画面を非表示
            rootPanel.SetActive(false);
        }
    }
}
