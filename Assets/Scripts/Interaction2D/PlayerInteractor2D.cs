using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InteractionSystem2D
{
    /// <summary>
    /// プレイヤー側のインタラクト検知・実行コンポーネント。
    /// - Input System の複数アクションを監視し、論理キーに変換して対象に渡す
    /// - 対象オブジェクトが要求する論理キーと一致した場合のみインタラクト実行
    /// - 検知用コライダー（IsTrigger=true）を別途アサインして使用
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlayerInteractor2D : MonoBehaviour
    {
        [Header("検知設定")]
        [Tooltip("インタラクト判定対象のレイヤー")]
        [SerializeField] private LayerMask interactableLayerMask;

        [Tooltip("検知用コライダー（IsTrigger=true の別コライダー推奨）。未指定なら自身のコライダーを使用（警告が出ます）")]
        [SerializeField] private Collider2D detectionCollider;

        [Tooltip("OverlapCircle使用時の半径（Collider未使用時）")]
        [SerializeField] private float detectionRadius = 1.5f;

        [Tooltip("true: Trigger検知 / false: OverlapCircle検知")]
        [SerializeField] private bool useTriggerCollider = true;

        [Header("入力・キー設定")]
        [Tooltip("InputAction名 -> 論理キー名 のマッピング。\n例: Action「Interact」-> Key「Interact」、Action「Talk」-> Key「Talk」")]
        [SerializeField] private List<ActionKeyMapping> actionKeyMappings = new List<ActionKeyMapping>
        {
            new ActionKeyMapping { actionName = "Interact", logicalKey = "Interact" },
            new ActionKeyMapping { actionName = "Talk", logicalKey = "Talk" }
        };

        [Header("デバッグ")]
        [SerializeField] private bool showDebugGizmos = true;

        // 内部状態
        private readonly List<IInteractable2D> _candidates = new List<IInteractable2D>();
        private IInteractable2D _currentTarget;
        private Collider2D _activeDetectionCollider;
        private PlayerInput _playerInput;
        private readonly Dictionary<string, InputAction> _actionMap = new Dictionary<string, InputAction>();
        private readonly Dictionary<InputAction, string> _actionToLogicalKey = new Dictionary<InputAction, string>();

        [System.Serializable]
        public class ActionKeyMapping
        {
            public string actionName;   // Input Actions アセットで定義したアクション名
            public string logicalKey;   // IInteractable2D.RequiredKey と一致させる論理キー
        }

        private void Awake()
        {
            _activeDetectionCollider = detectionCollider != null ? detectionCollider : GetComponent<Collider2D>();

            if (useTriggerCollider && _activeDetectionCollider != null)
            {
                if (!_activeDetectionCollider.isTrigger)
                {
                    Debug.LogWarning($"[PlayerInteractor2D] 検知用コライダー '{_activeDetectionCollider.name}' が Trigger ではありません。強制的に Trigger に設定します。", _activeDetectionCollider);
                    _activeDetectionCollider.isTrigger = true;
                }
            }
            else if (_activeDetectionCollider == GetComponent<Collider2D>())
            {
                Debug.LogWarning("[PlayerInteractor2D] 検知用コライダーがメインの物理コライダーと同一です。物理衝突が無効化されすり抜けの原因になります。子オブジェクトに別途 Trigger コライダーを作成しアサインしてください。", this);
            }

            _playerInput = GetComponent<PlayerInput>();
            if (_playerInput == null || _playerInput.actions == null)
            {
                Debug.LogError("[PlayerInteractor2D] PlayerInput コンポーネントまたは Actions アセットが見つかりません。", this);
                return;
            }

            // アクションの取得とマッピング構築
            foreach (var mapping in actionKeyMappings)
            {
                var action = _playerInput.actions.FindAction(mapping.actionName);
                if (action != null)
                {
                    _actionMap[mapping.actionName] = action;
                    _actionToLogicalKey[action] = mapping.logicalKey;
                    action.performed += OnActionPerformed;
                    action.Enable();
                }
                else
                {
                    Debug.LogWarning($"[PlayerInteractor2D] アクション '{mapping.actionName}' が Input Actions に見つかりません。", this);
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var kvp in _actionToLogicalKey)
            {
                kvp.Key.performed -= OnActionPerformed;
            }
        }

        private void FixedUpdate()
        {
            if (!useTriggerCollider)
            {
                RefreshNearestTarget();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!useTriggerCollider) return;
            if (other != _activeDetectionCollider) return;
            if (!IsInInteractableLayer(other.gameObject.layer)) return;

            var interactable = other.GetComponent<IInteractable2D>();
            if (interactable != null && !_candidates.Contains(interactable))
            {
                _candidates.Add(interactable);
                RefreshNearestTarget();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!useTriggerCollider) return;
            if (other != _activeDetectionCollider) return;

            var interactable = other.GetComponent<IInteractable2D>();
            if (interactable != null && _candidates.Contains(interactable))
            {
                _candidates.Remove(interactable);
                RefreshNearestTarget();
            }
        }

        /// <summary>
        /// 入力アクション実行時の共通ハンドラ
        /// </summary>
        private void OnActionPerformed(InputAction.CallbackContext context)
        {
            if (_currentTarget == null) return;

            // 実行されたアクションに対応する論理キーを取得
            if (_actionToLogicalKey.TryGetValue(context.action, out string logicalKey))
            {
                // 対象がそのキーを処理できるか試行
                if (_currentTarget.TryInteract(logicalKey))
                {
                    // 処理された場合、必要ならここでエフェクトやサウンドを鳴らす
                }
            }
        }

        private void RefreshNearestTarget()
        {
            IInteractable2D nearest = null;
            float minDistSqr = float.MaxValue;
            Vector2 playerPos = transform.position;

            if (!useTriggerCollider)
            {
                _candidates.Clear();
                var hits = Physics2D.OverlapCircleAll(playerPos, detectionRadius, interactableLayerMask);
                foreach (var hit in hits)
                {
                    var interactable = hit.GetComponent<IInteractable2D>();
                    if (interactable != null) _candidates.Add(interactable);
                }
            }

            foreach (var candidate in _candidates)
            {
                if (candidate is Component comp)
                {
                    float distSqr = (comp.transform.position - (Vector3)playerPos).sqrMagnitude;
                    if (distSqr < minDistSqr)
                    {
                        minDistSqr = distSqr;
                        nearest = candidate;
                    }
                }
            }

            _currentTarget = nearest;
        }

        private bool IsInInteractableLayer(int layer)
        {
            return (interactableLayerMask.value & (1 << layer)) != 0;
        }

        public IInteractable2D CurrentTarget => _currentTarget;

        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            Gizmos.color = Color.cyan;
            if (useTriggerCollider && _activeDetectionCollider != null)
            {
                if (_activeDetectionCollider is CircleCollider2D circle)
                {
                    Gizmos.DrawWireSphere(_activeDetectionCollider.transform.position + (Vector3)circle.offset, circle.radius * Mathf.Max(_activeDetectionCollider.transform.lossyScale.x, _activeDetectionCollider.transform.lossyScale.y));
                }
                else
                {
                    Gizmos.DrawWireCube(_activeDetectionCollider.bounds.center, _activeDetectionCollider.bounds.size);
                }
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, detectionRadius);
            }

            if (_currentTarget is Component targetComp)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, targetComp.transform.position);
            }
        }
    }
}
