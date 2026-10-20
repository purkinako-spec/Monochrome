using UnityEngine;
using UnityEngine.SceneManagement;
using Runtime.Dialogue.Interaction;

namespace MiniGame.Fish.Test2
{
    /// <summary>
    /// 釣りスポット調査 -> ミニゲーム開始/シーン遷移
    /// IInteractableProcessor (DialogueSystem標準インターフェース) を実装
    /// </summary>
    [AddComponentMenu("Interaction/Processors/Fishing Spot Processor")]
    public class FishingSpotProcessor : MonoBehaviour, IInteractableProcessor
    {
        [Header("Settings")]
        [SerializeField] private string _interactionText = "釣りをする";
        [SerializeField] private int _priority = 10;
        [SerializeField] private string _minigameSceneName = "FishingMinigameSceneTest1";

        // Player制御用インターフェース (プロジェクト側で定義推奨)
        // ここでは汎用的に MonoBehaviour の enabled を切るか、IPlayerControllable 等を想定
        private interface IPlayerControllable
        {
            bool Enabled { get; set; }
        }

        // --- IInteractableProcessor 実装 ---
        public bool CanInteract(GameObject interactor) => true; // 必要に応じてクエスト/アイテム判定追加
        public string GetInteractionText() => _interactionText;
        public int Priority => _priority;

        public void Interact(GameObject interactor)
        {
            Debug.Log($"[{name}] 釣りミニゲーム開始要求: {_minigameSceneName}");

            // 1. プレイヤー制御無効化 (GetComponent<PlayerMove>() 等の具象クラス依存を排除)
            // 対象が IPlayerControllable (または PlayerInput, MonoBehaviour) を実装している想定
            var playerController = interactor.GetComponent<IPlayerControllable>();
            if (playerController != null)
            {
                playerController.Enabled = false;
            }
            else
            {
                // フォールバック: PlayerInput コンポーネントがあれば無効化 (Input System 使用時)
                var playerInput = interactor.GetComponent<UnityEngine.InputSystem.PlayerInput>();
                if (playerInput != null) playerInput.enabled = false;
                
                // さらにフォールバック: 同一オブジェクトの MonoBehaviour を全無効化したくない場合は、
                // 専用の "PlayerController" スクリプトを取得して enabled=false にする実装に置き換えてください。
                Debug.LogWarning($"[{name}] IPlayerControllable または PlayerInput が見つかりません。手動で Player 制御を無効化してください。", interactor);
            }

            // 2. シーン遷移
            // SceneLoader がシングルトン/静的アクセス可能ならそれを使う (FindObjectOfType 非推奨)
            var sceneLoader = SceneLoader.Instance; // シングルトンプロパティ推奨
            if (sceneLoader == null)
            {
                // シングルトンでない場合はシーン内検索 (キャッシュ推奨)
                sceneLoader = Object.FindFirstObjectByType<SceneLoader>(); // Unity 2020+ / FindObjectOfType は非推奨
            }

            if (sceneLoader != null)
            {
                // シーン名を引数で渡せるように SceneLoader 側を修正しておくこと
                sceneLoader.LoadScene(_minigameSceneName);
            }
            else
            {
                // フォールバック: アドティブ読み込み
                Debug.LogWarning($"[{name}] SceneLoader が見つかりません。フォールバックで Additive 読み込みします。");
                SceneManager.LoadScene(_minigameSceneName, LoadSceneMode.Additive);
            }

            // 3. ミニゲーム側の初期化通知 (必要なら)
            // FishingMinigameManager.Instance?.StartMinigame(interactor, this);
        }
    }

    // 補足: SceneLoader の想定インターフェース (プロジェクト側で実装要)
    // public class SceneLoader : MonoBehaviour 
    // {
    //     public static SceneLoader Instance { get; private set; }
    //     public void LoadScene(string sceneName) { ... }
    // }
}
