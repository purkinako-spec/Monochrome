using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Monochrome.Core.Transition;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

namespace Monochrome.Core.Minigame
{
    [Serializable]
    public class MinigameLaunchRequest
    {
        [Tooltip("加算（Additive）ロードするミニゲームシーン名")]
        public string minigameSceneName = "FishingMinigameSceneTest1";

        [Tooltip("クリア時に 1 をセットするフラグキー（例: 'minigame_fishing_cleared'）")]
        public string clearFlagKey = "minigame_fishing_cleared";

        [Tooltip("ミニゲーム終了後に自動再生する会話コンテナ名（任意）")]
        public string resultDialogueContainer = "";

        [Tooltip("フェード暗転を挟むかどうか")]
        public bool useFade = true;

        [Tooltip("ミニゲーム終了時のコールバック")]
        public Action onComplete = null;
    }

    /// <summary>
    /// フィールドからミニゲームへのシームレスな突入・結果反映・復帰を統括するマネージャー。
    /// - フィールド画面・音声の一時休止
    /// - Additive シーンロード
    /// - ミニゲーム終了の自動検知
    /// - FlagManager への結果反映
    /// - フィールド復帰 ＆ 結果会話の自動再生
    /// を安全に一元管理します。
    /// </summary>
    [AddComponentMenu("Monochrome/Minigame/Minigame Connector")]
    public class MinigameConnector : MonoBehaviour
    {
        private static MinigameConnector instance;

        public static MinigameConnector Instance
        {
            get
            {
                if (instance == null || !instance)
                {
                    instance = FindFirstObjectByType<MinigameConnector>();
                    if ((instance == null || !instance) && Application.isPlaying)
                    {
                        var go = new GameObject("[MinigameConnector]");
                        instance = go.AddComponent<MinigameConnector>();
                        if (go.transform.parent == null) DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        private bool isMinigameRunning = false;
        private MinigameLaunchRequest currentRequest;

        public bool IsMinigameRunning => isMinigameRunning;

        private void Awake()
        {
            if (instance == null || !instance)
            {
                instance = this;
                if (transform.parent == null) DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// ミニゲームを起動します
        /// </summary>
        public async void LaunchMinigame(MinigameLaunchRequest request)
        {
            if (isMinigameRunning)
            {
                Debug.LogWarning("[MinigameConnector] 既にミニゲームが実行中です。");
                return;
            }

            isMinigameRunning = true;
            currentRequest = request;

            // 1. 必要に応じて暗転
            if (request.useFade)
            {
                await ScreenFader.Instance.FadeOutAsync(0.3f);
            }

            // 2. フィールドを休止（カメラOFF、音声停止、入力ロック）
            FieldPauseController.PauseField();

            // 3. シーンアンロード監視を登録
            SceneManager.sceneUnloaded += HandleSceneUnloaded;

            // 4. ミニゲームシーンを加算（Additive）ロード
            var op = SceneManager.LoadSceneAsync(request.minigameSceneName, LoadSceneMode.Additive);
            while (!op.isDone) await Task.Yield();

            // 5. フェードインしてミニゲーム開始
            if (request.useFade)
            {
                await ScreenFader.Instance.FadeInAsync(0.3f);
            }

            Debug.Log($"[MinigameConnector] ミニゲーム '{request.minigameSceneName}' を開始しました。");
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (currentRequest == null || scene.name != currentRequest.minigameSceneName) return;

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            OnMinigameFinished();
        }

        private async void OnMinigameFinished()
        {
            Debug.Log($"[MinigameConnector] ミニゲーム '{currentRequest.minigameSceneName}' の終了を検知しました。復帰処理を開始します。");

            // 1. 必要なら暗転
            if (currentRequest.useFade)
            {
                await ScreenFader.Instance.FadeOutAsync(0.2f);
            }

            // 2. フラグの更新
            if (!string.IsNullOrEmpty(currentRequest.clearFlagKey) && FlagManager.Instance != null)
            {
                FlagManager.Instance.SetBool(currentRequest.clearFlagKey, true);
                Debug.Log($"[MinigameConnector] クリアフラグをセットしました: {currentRequest.clearFlagKey} = true");
            }

            // 3. フィールドの復帰（カメラON、音声再開、入力ロック解除）
            FieldPauseController.ResumeField();

            // 4. フェードイン
            if (currentRequest.useFade)
            {
                await ScreenFader.Instance.FadeInAsync(0.3f);
            }

            var finishedRequest = currentRequest;
            currentRequest = null;
            isMinigameRunning = false;

            finishedRequest.onComplete?.Invoke();

            // 5. 結果会話の自動再生
            if (!string.IsNullOrEmpty(finishedRequest.resultDialogueContainer))
            {
                StartResultDialogue(finishedRequest.resultDialogueContainer);
            }
        }

        private void StartResultDialogue(string containerName)
        {
            if (DialogueManager.Instance == null) return;

            if (DialogueContainerManager.Instance != null && DialogueContainerManager.Instance.IsLoaded(containerName))
            {
                var container = DialogueContainerManager.Instance.GetLoaded(containerName);
                if (container != null)
                {
                    DialogueManager.Instance.StartDialogue(container);
                    return;
                }
            }
            Debug.Log($"[MinigameConnector] ミニゲーム後の結果会話を要求しました: {containerName}");
        }
    }
}
