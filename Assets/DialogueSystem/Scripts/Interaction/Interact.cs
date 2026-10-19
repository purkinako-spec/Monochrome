using UnityEngine;
using UnityEngine.InputSystem;
using Runtime.Dialogue.Logic;

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

            // 起動時に View を解決し、DialogueManager (Singleton) に登録しておく
            // 手動アサインフィールドを廃止し、自動検索 (FindObjectOfType) のみに依存
            ResolveAndRegisterDialogueView();
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
        /// インタラクト入力検知
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
        /// シーンから IDialogueView を探し、DialogueManager に登録する
        /// </summary>
        private void ResolveAndRegisterDialogueView()
        {
            // 非アクティブ含めて検索 (手動アサインフィールドは廃止)
            var view = FindObjectOfType<MonoBehaviour>(true) as IDialogueView;

            if (view != null && DialogueManager.Instance != null)
            {
                _cachedDialogueView = view;
                DialogueManager.Instance.RegisterView(view);
                Debug.Log($"[Interact] DialogueManager に View を登録しました: {view.GetType().Name}");
            }
            else if (view == null)
            {
                Debug.LogWarning("[Interact] IDialogueView 実装オブジェクトがシーンに見つかりませんでした。");
            }
            else
            {
                Debug.LogWarning("[Interact] DialogueManager.Instance がまだ存在しません。");
            }
        }

        /// <summary>
        /// 外部から View を強制設定する場合用 (動的な切り替え等)
        /// </summary>
        public void SetDialogueView(IDialogueView view) 
        {
            _cachedDialogueView = view;
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.RegisterView(view);
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
