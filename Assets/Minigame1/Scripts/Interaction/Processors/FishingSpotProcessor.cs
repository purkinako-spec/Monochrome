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

            // 1. プレイヤー制御無効化
            DisablePlayerControl(interactor);

            // 2. シーン遷移処理
            LoadMinigameScene();
        }

        private void DisablePlayerControl(GameObject interactor)
        {
            // 独自インターフェース優先
            var playerController = interactor.GetComponent<IPlayerControllable>();
            if (playerController != null)
            {
                playerController.Enabled = false;
                return;
            }

            // Input System の PlayerInput コンポーネントがあれば無効化
            var playerInput = interactor.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = false;
                return;
            }

            // 汎用的な MonoBehaviour 無効化は避け、警告のみ出す (プロジェクト固有の PlayerController 等を取得して無効化する実装に置き換えてください)
            Debug.LogWarning($"[{name}] IPlayerControllable または PlayerInput が見つかりません。Player 制御の無効化を手動で実装してください。", interactor);
        }

        private void LoadMinigameScene()
        {
            // プロジェクト固有の SceneLoader が存在するか確認 (リフレクションや共通インターフェース経由が望ましいが、ここでは型名で検索)
            // ※ 実際のプロジェクトでは "SceneLoader" クラスを共通アセンブリに定義し、参照するのが正解です。
            var sceneLoaderType = System.Type.GetType("SceneLoader") 
                                ?? System.Type.GetType("MiniGame.Fish.Test2.SceneLoader")
                                ?? System.Type.GetType("Runtime.SceneLoader");

            if (sceneLoaderType != null)
            {
                // 静的 Instance プロパティまたはメソッドをリフレクションで呼び出し試行
                var instanceProp = sceneLoaderType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var loadMethod = sceneLoaderType.GetMethod("LoadScene", new[] { typeof(string) });

                if (instanceProp != null && loadMethod != null)
                {
                    var instance = instanceProp.GetValue(null);
                    if (instance != null)
                    {
                        loadMethod.Invoke(instance, new object[] { _minigameSceneName });
                        return;
                    }
                }
            }

            // フォールバック: アドティブ読み込み (標準機能)
            Debug.LogWarning($"[{name}] SceneLoader が見つからないか Instance/LoadScene が不正です。標準の Additive 読み込みでフォールバックします。");
            SceneManager.LoadScene(_minigameSceneName, LoadSceneMode.Additive);
        }
    }
}
