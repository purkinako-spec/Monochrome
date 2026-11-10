using Runtime.Dialogue.Logic;
using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Monochrome.Core.Character;
using Monochrome.Core.Input;
using Runtime.Dialogue.Core;

namespace Monochrome.Core.Transition
{
    public enum TransitionStyle
    {
        FadeOnly,       // 暗転のみ（同一シーンや小部屋用）
        LoadingScreen   // ロード画面表示（大シーン間移動用）
    }

    /// <summary>
    /// エリア移動のリクエストパラメータ
    /// </summary>
    [Serializable]
    public class TransitionRequest
    {
        [Tooltip("遷移先シーン名（空の場合は現在の同一シーン内でワープ）")]
        public string destinationScene = "";

        [Tooltip("移動先の SpawnPoint ID")]
        public string spawnPointId = "Default";

        [Tooltip("同一シーン移動時、SpawnPoint の代わりに直接 Transform を指定する場合")]
        public Transform targetTransform = null;

        [Tooltip("暗転のみか、ロード画面を挟むか")]
        public TransitionStyle style = TransitionStyle.FadeOnly;

        [Tooltip("フェードの所要時間（秒）")]
        public float fadeDuration = 0.4f;

        [Tooltip("移動完了後に自動再生する会話コンテナ名（任意）")]
        public string resumeDialogueContainer = "";

        [Tooltip("移動完了時のコールバック")]
        public Action onComplete = null;
    }

    /// <summary>
    /// エリア遷移（シーン内ワープ / シーン間ロード）を一括統括するシングルトンマネージャー。
    /// - プレイヤー操作の自動ロック/解除
    /// - 暗転フェードおよび非同期ロード画面
    /// - Cinemachine カメラの瞬間追従ワープ
    /// - 移動後の会話自動再生
    /// を安全に実行します。
    /// </summary>
    [AddComponentMenu("Monochrome/Transition/Area Transition Manager")]
    [DisallowMultipleComponent]
    public class AreaTransitionManager : MonoBehaviour
    {
        private static AreaTransitionManager instance;

        public static AreaTransitionManager Instance
        {
            get
            {
                if (instance == null || !instance)
                {
                    instance = FindFirstObjectByType<AreaTransitionManager>();
                    if ((instance == null || !instance) && Application.isPlaying)
                    {
                        var go = new GameObject("[AreaTransitionManager]");
                        instance = go.AddComponent<AreaTransitionManager>();
                        if (go.transform.parent == null) DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        private bool isTransitioning = false;
        public bool IsTransitioning => isTransitioning;

        public event Action<string> OnSceneTransitionStarted;
        public event Action<string> OnSceneTransitionCompleted;

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
        /// エリア移動を実行します
        /// </summary>
        public async void Transition(TransitionRequest request)
        {
            if (isTransitioning)
            {
                Debug.LogWarning("[AreaTransitionManager] 既にエリア移動処理が実行中です。重複リクエストを無視します。");
                return;
            }

            isTransitioning = true;
            OnSceneTransitionStarted?.Invoke(request.destinationScene);

            var player = PlayerController2D.LocalPlayer;

            // 1. プレイヤー操作をロック
            if (player != null)
            {
                player.BlockState.AddBlocker(ActionBlocker.Transition);
                player.Motor.StopImmediately();
            }

            // 2. フェードアウト
            await ScreenFader.Instance.FadeOutAsync(request.fadeDuration);

            // 3. シーン遷移 または 同一シーン内ワープ
            bool isInterScene = !string.IsNullOrEmpty(request.destinationScene) &&
                                request.destinationScene != SceneManager.GetActiveScene().name;

            if (isInterScene)
            {
                if (request.style == TransitionStyle.LoadingScreen)
                {
                    await LoadingScreenUI.Instance.LoadSceneAsyncWithProgress(request.destinationScene);
                }
                else
                {
                    var op = SceneManager.LoadSceneAsync(request.destinationScene, LoadSceneMode.Single);
                    while (!op.isDone) await Task.Yield();
                }
            }
            else
            {
                // 同一シーン内の場合は一瞬の余韻
                await Task.Delay(100);
            }

            // 4. 移動先座標と向きの決定
            Vector2 targetPos = Vector2.zero;
            Vector2? targetFacing = null;

            if (request.targetTransform != null)
            {
                targetPos = request.targetTransform.position;
            }
            else
            {
                var spawn = SpawnPoint.Find(request.spawnPointId);
                if (spawn != null)
                {
                    targetPos = spawn.Position;
                    targetFacing = spawn.FacingDirection;
                }
                else
                {
                    Debug.LogWarning($"[AreaTransitionManager] SpawnPoint '{request.spawnPointId}' が見つかりませんでした。原点へ配置します。");
                }
            }

            // 5. プレイヤー配置 & カメラワープ
            player = PlayerController2D.LocalPlayer;
            if (player != null)
            {
                Vector3 delta = (Vector3)targetPos - player.transform.position;
                player.Motor.WarpTo(targetPos, targetFacing);
                WarpCinemachineCameras(delta);
            }

            // 6. フェードイン
            await ScreenFader.Instance.FadeInAsync(request.fadeDuration);

            // 7. プレイヤー操作ロック解除
            if (player != null)
            {
                player.BlockState.RemoveBlocker(ActionBlocker.Transition);
            }

            isTransitioning = false;
            OnSceneTransitionCompleted?.Invoke(request.destinationScene);
            request.onComplete?.Invoke();

            // 8. 移動後の会話自動再生
            if (!string.IsNullOrEmpty(request.resumeDialogueContainer))
            {
                StartResumeDialogue(request.resumeDialogueContainer);
            }
        }

        /// <summary>
        /// Cinemachine のバーチャルカメラが画面外からピューッと飛ばないよう、瞬間移動を通知します
        /// </summary>
        private void WarpCinemachineCameras(Vector3 delta)
        {
            // Cinemachine がインポートされている場合、OnTargetObjectWarped をリフレクションで安全に実行
            try
            {
                var vcamType = Type.GetType("Unity.Cinemachine.CinemachineVirtualCamera, Unity.Cinemachine") ??
                               Type.GetType("Cinemachine.CinemachineVirtualCamera, Cinemachine");
                if (vcamType != null && PlayerController2D.LocalPlayer != null)
                {
                    var vcams = FindObjectsByType(vcamType, FindObjectsSortMode.None);
                    var warpMethod = vcamType.GetMethod("OnTargetObjectWarped", new[] { typeof(Transform), typeof(Vector3) });
                    if (warpMethod != null)
                    {
                        foreach (var vcam in vcams)
                        {
                            warpMethod.Invoke(vcam, new object[] { PlayerController2D.LocalPlayer.transform, delta });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AreaTransitionManager] Cinemachine カメラワープ通知スキップ: {ex.Message}");
            }
        }

        private void StartResumeDialogue(string containerName)
        {
            if (DialogueManager.Instance == null) return;

            // DialogueContainerManager から非同期取得または直接ロード
            if (DialogueContainerManager.Instance != null && DialogueContainerManager.Instance.IsLoaded(containerName))
            {
                var container = DialogueContainerManager.Instance.GetLoaded(containerName);
                if (container != null)
                {
                    DialogueManager.Instance.StartDialogue(container);
                    return;
                }
            }

            Debug.Log($"[AreaTransitionManager] 移動先での会話自動再生を要求しました: {containerName}");
        }
    }
}
