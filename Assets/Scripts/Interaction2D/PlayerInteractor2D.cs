using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InteractionSystem2D
{
    /// <summary>
    /// プレイヤー側のインタラクト検知・実行コンポーネント。
    /// - CircleCollider2D(IsTrigger) または Physics2D.OverlapCircle で範囲検知
    /// - LayerMask でインタラクト専用レイヤーのみを判定
    /// - 範囲内に複数ある場合は「最も近いオブジェクト」を優先
    /// - 新Input Systemのイベント駆動（performed）で入力を受け取る
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlayerInteractor2D : MonoBehaviour
    {
        [Header("検知設定")]
        [Tooltip("インタラクト判定対象のレイヤー（専用レイヤーを推奨）")]
        [SerializeField] private LayerMask interactableLayerMask;

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
        // 自身のコライダー（Trigger用）
        private Collider2D _selfCollider;
        // Input Action参照
        private InputAction _interactAction;

        private void Awake()
        {
            _selfCollider = GetComponent<Collider2D>();
            // コライダーをTriggerに強制設定（検知用途のため）
            if (useTriggerCollider && _selfCollider != null)
            {
                _selfCollider.isTrigger = true;
            }

            // Input Systemのアクションを取得（PlayerInputコンポーネントから、または直接アセット参照でも可）
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
            if (!useTriggerCollider) return;
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
            Vector2 playerPos = transform.position;

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
                // IInteractable2D は Component を継承している前提で transform にアクセス
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
            if (useTriggerCollider && _selfCollider != null)
            {
                // CircleCollider2D の場合のみ半径を描画
                if (_selfCollider is CircleCollider2D circle)
                {
                    Gizmos.DrawWireSphere(transform.position + (Vector3)circle.offset, circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y));
                }
                else
                {
                    Gizmos.DrawWireCube(transform.position, _selfCollider.bounds.size);
                }
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, detectionRadius);
            }

            // 現在のターゲットへ線を引く
            if (_currentTarget is Component targetComp)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, targetComp.transform.position);
            }
        }
    }
}
