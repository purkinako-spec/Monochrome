using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InteractionSystem2D
{
    /// <summary>
    /// プレイヤー側のインタラクト検知・実行コンポーネント。
    /// - PlayerInput の標準的な仕組み（C# イベント）を使用
    /// - アクション名をそのまま「論理キー」として扱う（規約ベース：Action名 == LogicalKey）
    /// - 別途マッピングリストをインスペクタで設定する必要なし
    /// - 検知用コライダー（IsTrigger=true）を別途アサインして使用
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(PlayerInput))]
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

        [Header("入力設定")]
        [Tooltip("監視するアクションマップ名（空の場合は全マップの全アクションを監視）")]
        [SerializeField] private string targetActionMapName = "Player";

        [Header("デバッグ")]
        [SerializeField] private bool showDebugGizmos = true;

        public InputActionReference action;

        // 内部状態
        private readonly List<IInteractable2D> _candidates = new List<IInteractable2D>();
        private IInteractable2D _currentTarget;
        private Collider2D _activeDetectionCollider;
        private PlayerInput _playerInput;
        private readonly Dictionary<InputAction, string> _actionToLogicalKey = new Dictionary<InputAction, string>();
        private bool _isSubscribed = false;

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
            if (_playerInput == null)
            {
                Debug.LogError("[PlayerInteractor2D] PlayerInput コンポーネントが見つかりません。", this);
            }
        }

        private void Start()
        {
            // PlayerInput の Actions 初期化完了を待って購読
            SubscribeActions();
        }

        private void OnDestroy()
        {
            UnsubscribeActions();
        }

        private void OnDisable()
        {
            UnsubscribeActions();
        }

        private void OnEnable()
        {
            // 再有効化時に購読が外れていれば再購読
            if (!_isSubscribed && _playerInput != null)
            {
                SubscribeActions();
            }
        }

        /// <summary>
        /// 対象のアクションマップ内の全アクションを購読する
        /// </summary>
        private void SubscribeActions()
        {
            if (_playerInput == null || _playerInput.actions == null)
            {
                Debug.LogWarning("[PlayerInteractor2D] PlayerInput または Actions アセットが未設定です。", this);
                return;
            }

            IEnumerable<InputAction> actionsToSubscribe;

            if (string.IsNullOrEmpty(targetActionMapName))
            {
                // 全マップ対象: InputActionAsset から全アクションマップの全アクションを取得
                actionsToSubscribe = _playerInput.actions.actionMaps.SelectMany(map => map.actions);
            }
            else
            {
                // 指定マップのみ対象
                var map = _playerInput.actions.FindActionMap(targetActionMapName);
                if (map != null)
                {
                    actionsToSubscribe = map.actions;
                }
                else
                {
                    Debug.LogWarning($"[PlayerInteractor2D] アクションマップ '{targetActionMapName}' が見つかりません。", this);
                    return;
                }
            }

            foreach (var action in actionsToSubscribe)
            {
                // すでに購読済みならスキップ
                if (_actionToLogicalKey.ContainsKey(action)) continue;

                _actionToLogicalKey[action] = action.name; // アクション名をそのまま論理キーとして使用
                action.performed += OnActionPerformed;
                action.Enable();
            }

            _isSubscribed = true;
        }

        private void UnsubscribeActions()
        {
            foreach (var kvp in _actionToLogicalKey)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.performed -= OnActionPerformed;
                }
            }
            _actionToLogicalKey.Clear();
            _isSubscribed = false;
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

            // 実行されたアクション名を論理キーとして取得
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
