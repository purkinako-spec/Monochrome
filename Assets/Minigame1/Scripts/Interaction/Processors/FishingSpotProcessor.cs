using UnityEngine;
using UnityEngine.SceneManagement;
using Runtime.Dialogue.Interaction;

namespace MiniGame.Fish.Test2
{
    /// <summary>
    /// Script 3 実装: 釣りスポット調査 -> ミニゲーム開始/シーン遷移
    /// </summary>
    [AddComponentMenu("Interaction/Processors/Fishing Spot Processor")]
    public class FishingSpotProcessor : MonoBehaviour, IInteractableProcessor
    {
        [Header("Settings")]
        [SerializeField] private string _interactionText = "釣りをする";
        [SerializeField] private int _priority = 10; // 会話より優先度高め等
        [SerializeField] private string _minigameSceneName = "FishingMinigameSceneTest1";

        // --- IInteractableProcessor 実装 ---
        public bool CanInteract(GameObject interactor) => true; // 必要なら条件追加 (アイテム所持チェック等)
        public string GetInteractionText() => _interactionText;
        public int Priority => _priority;

        public void Interact(GameObject interactor)
        {
            Debug.Log($"[{name}] 釣りミニゲーム開始要求: {_minigameSceneName}");

            // 1. プレイヤー制御無効化 (PlayerMove 等があれば)
            var playerMove = interactor.GetComponent<PlayerMove>();
            if (playerMove != null) playerMove.enabled = false; // または SetCanMove(false) 等

            // 2. シーン遷移 or マネージャー起動
            // 既存の SceneLoader がある場合はそれを利用
            var sceneLoader = Object.FindObjectOfType<SceneLoader>(); // シングルトン化推奨
            if (sceneLoader != null)
            {
                sceneLoader.LoadScene(_minigameSceneName);
            }
            else
            {
                // フォールバック: アドティブ読み込み
                SceneManager.LoadScene(_minigameSceneName, LoadSceneMode.Additive);
            }

            // 3. ここでミニゲーム側の初期化イベントを飛ばす等の処理が必要なら追加
            // FishingMinigameManager.Instance?.StartMinigame(...);
        }
    }
}
