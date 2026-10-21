using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InteractionSystem2D
{
    /// <summary>
    /// プレイヤー側のインタラクト検知・実行コンポーネント。
    /// - 検知用コライダー（IsTrigger=true）を別途アサインして使用する設計に変更
    /// - メインの物理コライダー（IsTrigger=false）は触らないため、壁やNPCとすり抜けなくなる
    /// </summary>
    [RequireComponent(typeof(Collider2D))] // メインの物理コライダー（身体）が必須
    public class PlayerInteractor2D : MonoBehaviour
    {
        [Header("検知設定")]
        [Tooltip("インタラクト判定対象のレイヤー（専用レイヤーを推奨）")]
        [SerializeField] private LayerMask interactableLayerMask;

        [Tooltip("検知用コライダー（IsTrigger=true の別コライダー、または子オブジェクトのコライダーを指定）。未指定の場合はこのオブジェクトのコライダーを使用しますが、物理判定と干渉しないよう別コライダー推奨。")]
        [SerializeField] private Collider2D detectionCollider;

        [Tooltip("OverlapCircleを使用する場合の半径（Colliderを使わない場合）")]
        [SerializeField] private float detectionRadius = 1.5f;

        [Tooltip("true: 物理コライダー(Trigger)で検知 / false: Physics2D.OverlapCircleで毎フレーム検知")]
        [SerializeField] private bool useTriggerCollider = true;

        [Header("デバッグ")]
        [SerializeField] private bool showDebugGizmos = true;

        // 現在範囲内にあるインタラクト可能オブジェクトの候補
        private readonly List<IInteractable2D> _candidates = new List<IInteractable2D>();
        // 現在選択中（最も近い）のインタラクト対象
        private IInteractable2D _currentTarget;
        // 検知用コライダー（Trigger用）
        private Collider2D _activeDetectionCollider;
        // Input Action参照
        private InputAction _interactAction;

        private void Awake()
        {
            // 検知用コライダーの決定
            _activeDetectionCollider = detectionCollider != null ? detectionCollider : GetComponent<Collider2D>();

            // 検知用コライダーが設定されている場合、強制的にTriggerにする
            if (useTriggerCollider && _activeDetectionCollider != null)
            {
                if (!_activeDetectionCollider.isTrigger)
                {
                    Debug.LogWarning($"[PlayerInteractor2D] 検知用コライダー '{_activeDetectionCollider.name}' が Trigger ではありません。強制的に Trigger に設定します。", _activeDetectionCollider);
                    _activeDetectionCollider.isTrigger = true;
                }
            }
            // 検知用コライダーがメインのコライダーと同一の場合、警告（すり抜けの原因になるため）
            else if (_activeDetectionCollider == GetComponent<Collider2D>())
            {
                /*Debug.LogWarning("[PlayerInteractor2D] 検知用コライダーがメインの物理コライダーと同一です。" +
                                 "物理衝突（壁あたり判定）が無効化され、すり抜けの原因になります。" +
                                 "子オブジェクトに別途 CircleCollider2D (IsTrigger=ON) を作成し、Detection Collider にアサインすることを強く推奨します。", this);
                */
            }

            // Input Systemのアクションを取得
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                _interactAction = playerInput.actions["Interact"];
            }

            if (_interactAction == null)
            {
                Debug.LogWarning("[PlayerInteractor2D] 'Interact' アクションが見つかりません。PlayerInput の Actions またはインスペクタで設定してください。");
            }
        }

        private void OnEnable()
        {
            if (_interactAction != null)
            {
                _interactAction.performed += OnInteractPerformed;
                _interactAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (_interactAction != null)
            {
                _interactAction.performed -= OnInteractPerformed;
                _interactAction.Disable();
            }
        }

        private void FixedUpdate()
        {
            // OverlapCircleモードの場合、毎フレーム（FixedUpdate）で最近傍を再計算
            if (!useTriggerCollider)
            {
                RefreshNearestTarget();
            }
        }

        /// <summary>
        /// Triggerモード時：範囲内に入ったオブジェクトを候補に追加
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            // このスクリプトがアタッチされているコライダー（検知用）以外からのイベントは無視
            if (!useTriggerCollider) return;
            if (other != _activeDetectionCollider) return; // 自身の検知コライダー以外のTriggerイベントを無視
            if (!IsInInteractableLayer(other.gameObject.layer)) return;

            var interactable = other.GetComponent<IInteractable2D>();
            if (interactable != null && !_candidates.Contains(interactable))
            {
                _candidates.Add(interactable);
                RefreshNearestTarget();
            }
        }

        /// <summary>
        /// Triggerモード時：範囲外に出たオブジェクトを候補から除外
        /// </summary>
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
        /// 入力イベント（performed）から呼ばれるインタラクト実行
        /// </summary>
        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            if (_currentTarget != null)
            {
                _currentTarget.Interact();
            }
        }

        /// <summary>
        /// 候補リストから「プレイヤーに最も近い」オブジェクトを1つ選択して _currentTarget に設定
        /// </summary>
        private void RefreshNearestTarget()
        {
            IInteractable2D nearest = null;
            float minDistSqr = float.MaxValue;
            Vector2 playerPos = transform.position; // プレイヤーの中心座標を基準に距離計算

            // OverlapCircleモードの場合、ここで物理クエリを実行して候補を更新
            if (!useTriggerCollider)
            {
                _candidates.Clear();
                var hits = Physics2D.OverlapCircleAll(playerPos, detectionRadius, interactableLayerMask);
                foreach (var hit in hits)
                {
                    var interactable = hit.GetComponent<IInteractable2D>();
                    if (interactable != null)
                    {
                        _candidates.Add(interactable);
                    }
                }
            }

            // 候補の中から最近傍を選択
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

        /// <summary>
        /// レイヤーが判定対象マスクに含まれるか判定
        /// </summary>
        private bool IsInInteractableLayer(int layer)
        {
            return (interactableLayerMask.value & (1 << layer)) != 0;
        }

        /// <summary>
        /// 現在のターゲットを外部（UI等）から取得するためのプロパティ
        /// </summary>
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
