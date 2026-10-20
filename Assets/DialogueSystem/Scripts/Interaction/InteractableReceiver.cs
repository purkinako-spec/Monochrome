using UnityEngine;
using Runtime.Dialogue.Interaction;

namespace Runtime.Dialogue.Interaction
{
    /// <summary>
    /// Script 2: インタラクト対象オブジェクトにアタッチ
    /// Player(Script 1: PlayerInteractor) から通知を受け、インスペクタで指定した Processor(Script 3) に委譲する
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class InteractableReceiver : MonoBehaviour
    {
        [Header("Processor (Script 3 Implementation)")]
        [Tooltip("このオブジェクトで実行したい具体的な処理(IInteractableProcessor実装)をインスペクタで選択・アサイン")]
        // [SerializeReference] は UnityEngine.Object 継承クラス (MonoBehaviour等) には使えないため [SerializeField] を使用
        [SerializeField] 
        private IInteractableProcessor _processor;

        // --- Public API (Script 1: PlayerInteractor から呼ばれる) ---

        public IInteractableProcessor Processor 
        { 
            get => _processor; 
            set => _processor = value; 
        }
        
        public string InteractionText => _processor?.GetInteractionText() ?? "Interact";
        public int Priority => _processor?.Priority ?? 0;

        public bool CanInteract(GameObject interactor) 
            => _processor != null && _processor.CanInteract(interactor);

        /// <summary>
        /// Script 1 (PlayerInteractor) から呼ばれるエントリーポイント
        /// </summary>
        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                Debug.LogWarning($"[{name}] インタラクト不可: Processor未設定 または CanInteract=false", this);
                return;
            }

            Debug.Log($"[{name}] インタラクト実行 -> {_processor.GetType().Name}");
            _processor.Interact(interactor);
        }

        private void Reset()
        {
            // 同一GameObject上のProcessorを自動探索してセット (利便性向上)
            // NPCInteractable が DialogueProcessor (MonoBehaviour) を付与しているため、それが拾われる
            if (_processor == null)
            {
                var found = GetComponent<IInteractableProcessor>();
                if (found != null) _processor = found;
            }
        }
    }
}
