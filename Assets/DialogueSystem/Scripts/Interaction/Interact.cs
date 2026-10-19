using UnityEngine;
using UnityEngine.InputSystem;
using Runtime.Dialogue.Logic;

namespace Runtime.Dialogue.Interaction
{
    /// <summary>
    /// プレイヤーにアタッチし、周囲の IInteractable を検出・インタラクト入力を伝達するコンポーネント
    /// View の登録は IDialogueView 実装側 (Awake 等) で DialogueManager.Instance.RegisterView(this) する想定
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Interact : MonoBehaviour
    {
        [Header("Detection Settings")]
        [Tooltip("検出半径")]
        [SerializeField] private float _detectionRadius = 1.5f;
        [Tooltip("インタラクト可能なレイヤー")]
        [SerializeField] private LayerMask _interactableLayer;
        [Tooltip("検出中心 (未指定なら自身のTransform)")]
        [SerializeField] private Transform _detectionCenter;

        [Header("Input System")]
        [Tooltip("インタラクト入力アクション (例: 'Interact' または 'Submit')")]
        [SerializeField] private InputActionReference _interactAction;

        private IInteractable _currentTarget;
        private Collider2D[] _overlapBuffer = new Collider2D[10];

        private void Awake()
        {
            if (_detectionCenter == null) _detectionCenter = transform;

            if (_interactAction != null)
            {
                _interactAction.action.Enable();
                _interactAction.action.performed += OnInteractInput;
            }
        }

        private void OnDestroy()
        {
            if (_interactAction != null)
            {
                _interactAction.action.performed -= OnInteractInput;
                _interactAction.action.Disable();
            }
        }

        private void Update()
        {
            DetectNearestInteractable();
        }

        /// <summary>
        /// 範囲内の最近接 IInteractable を検出
        /// </summary>
        private void DetectNearestInteractable()
        {
            int count = Physics2D.OverlapCircleNonAlloc(_detectionCenter.position, _detectionRadius, _overlapBuffer, _interactableLayer);

            IInteractable nearest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                var interactable = col.GetComponentInParent<IInteractable>();
                
                if (interactable != null)
                {
                    float dist = Vector2.Distance(_detectionCenter.position, col.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = interactable;
                    }
                }
            }

            if (_currentTarget != nearest)
            {
                _currentTarget = nearest;
                OnTargetChanged(_currentTarget);
            }
        }

        /// <summary>
        /// インタラクト入力検知 → 対象の Interact() を呼び出し (会話開始等は対象側で Manager.StartDialogue を叩く)
        /// </summary>
        private void OnInteractInput(InputAction.CallbackContext ctx)
        {
            if (_currentTarget != null)
            {
                _currentTarget.Interact(gameObject);
            }
        }

        /// <summary>
        /// ターゲット変更時の処理 (UIプロンプト表示など)
        /// </summary>
        private void OnTargetChanged(IInteractable newTarget)
        {
            if (newTarget != null)
            {
                Debug.Log($"[Interact] 対象検出: {newTarget.GetInteractionText()}");
            }
            else
            {
                Debug.Log("[Interact] 対象なし");
            }
        }

        /// <summary>
        /// 現在のターゲット取得 (UI側から参照用)
        /// </summary>
        public IInteractable CurrentTarget => _currentTarget;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            Vector3 center = _detectionCenter ? _detectionCenter.position : transform.position;
            Gizmos.DrawWireSphere(center, _detectionRadius);
        }
    }
}
