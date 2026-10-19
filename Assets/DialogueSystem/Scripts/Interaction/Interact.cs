using UnityEngine;
using UnityEngine.InputSystem;
using Runtime.Dialogue.Logic;
using IInteractable = global::IInteractable;

namespace Runtime.Dialogue.Interaction
{
    /// <summary>
    /// プレイヤーにアタッチし、周囲の IInteractable を検出・インタラクトするコンポーネント
    /// DialogueTesting.cs のパターン (RegisterView -> StartDialogue) に準拠
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

        [Header("Dialogue View Reference")]
        [Tooltip("シーン上の IDialogueView 実装オブジェクト (自動取得もしない場合は手動アサイン)")]
        [SerializeField] private MonoBehaviour _dialogueViewProvider;

        [Header("Debug")]
        [SerializeField] private bool _showGizmos = true;

        private IInteractable _currentTarget;
        private IDialogueView _cachedDialogueView;
        private Collider2D[] _overlapBuffer = new Collider2D[10];

        private void Awake()
        {
            if (_detectionCenter == null) _detectionCenter = transform;

            if (_interactAction != null)
            {
                _interactAction.action.Enable();
                _interactAction.action.performed += OnInteractInput;
            }

            ResolveDialogueView();
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

        private void OnInteractInput(InputAction.CallbackContext ctx)
        {
            if (_currentTarget != null)
            {
                _currentTarget.Interact(gameObject);
            }
        }

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

        private void ResolveDialogueView()
        {
            if (_dialogueViewProvider != null)
            {
                _cachedDialogueView = _dialogueViewProvider as IDialogueView;
                if (_cachedDialogueView == null)
                {
                    Debug.LogWarning($"[_dialogueViewProvider] に IDialogueView が実装されていません: {_dialogueViewProvider.name}");
                }
            }
            else
            {
                _cachedDialogueView = FindObjectOfType<MonoBehaviour>(true) as IDialogueView;
            }

            if (_cachedDialogueView != null)
            {
                Debug.Log($"[Interact] DialogueView をキャッシュしました: {_cachedDialogueView.GetType().Name}");
            }
        }

        public void SetDialogueView(IDialogueView view) => _cachedDialogueView = view;

        public IInteractable CurrentTarget => _currentTarget;

        private void OnDrawGizmosSelected()
        {
            if (!_showGizmos) return;
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            Vector3 center = _detectionCenter ? _detectionCenter.position : transform.position;
            Gizmos.DrawWireSphere(center, _detectionRadius);
        }
    }
}
