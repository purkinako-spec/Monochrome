using UnityEngine;

namespace InteractionSystem2D
{
    /// <summary>
    /// IInteractable2D のサンプル実装。
    /// インスペクタで「必要な論理キー」を指定し、一致した場合のみ反応する。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class InteractableObject2DExample : MonoBehaviour, IInteractable2D
    {
        [Header("インタラクト設定")]
        [Tooltip("このオブジェクトが反応する論理キー名（PlayerInteractor2D の ActionKeyMapping.logicalKey と一致させる）\n例: Interact, Talk, Inspect, Open")]
        [SerializeField] private string requiredKey = "Interact";

        [Tooltip("UIに表示されるプロンプトテキスト（キー名部分は実行時のバインディングに合わせて動的に変えたい場合は別途実装が必要）")]
        [SerializeField] private string interactPrompt = "[E] 調べる";

        [Tooltip("インタラクト可能かどうか")]
        [SerializeField] private bool isInteractable = true;

        [Header("内部参照")]
        [SerializeField] private Collider2D _collider;

        // --- IInteractable2D 実装 ---

        public string RequiredKey => requiredKey;

        public bool TryInteract(string key)
        {
            if (!isInteractable) return false;
            if (key != requiredKey) return false; // キーが一致しない場合は処理しない

            Interact();
            return true; // 処理した
        }

        public string GetInteractPrompt()
        {
            return isInteractable ? interactPrompt : string.Empty;
        }

        /// <summary>
        /// 実際のインタラクト処理（オーバーライドやイベントで拡張想定）
        /// </summary>
        protected virtual void Interact()
        {
            Debug.Log($"<color=cyan>[Interact]</color> {gameObject.name} がキー '{requiredKey}' でインタラクトされました。", this);
        }

        public void SetInteractable(bool state)
        {
            isInteractable = state;
        }

        // --- Unity Lifecycle ---

        private void Reset()
        {
            _collider = GetComponent<Collider2D>();

            // レイヤー自動設定
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1)
            {
                gameObject.layer = interactableLayer;
            }
            else
            {
                Debug.LogWarning($"[InteractableObject2DExample] 'Interactable' レイヤーが存在しません。Project Settings > Tags and Layers で追加してください。", this);
            }
        }

        private void OnValidate()
        {
            // 物理設定は手動管理推奨のため自動変更しない
        }
    }
}
