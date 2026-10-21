namespace InteractionSystem
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// プレイヤーのカメラ正面に Raycast を飛ばし、IInteractable を検知・実行するコンポーネント
    /// 新 Input System のイベント駆動 (performed) で動作し、Update で入力ポーリングを行わない
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Detection Settings")]
        [Tooltip("Raycast の最大距離")]
        [SerializeField] private float _interactDistance = 3f;
        
        [Tooltip("インタラクト対象とするレイヤー (Interactable 専用レイヤーを推奨)")]
        [SerializeField] private LayerMask _interactableLayerMask;

        [Tooltip("Raycast の原点となるカメラ (未設定時はメインカメラ)")]
        [SerializeField] private Camera _playerCamera;

        [Header("Input")]
        [Tooltip("インタラクト入力アクション (PlayerInput コンポーネント経由で自動バインドされる想定)")]
        [SerializeField] private InputActionReference _interactAction;

        [Header("Debug")]
        [SerializeField] private bool _showDebugRay = true;
        [SerializeField] private Color _hitColor = Color.green;
        [SerializeField] private Color _missColor = Color.red;

        // キャッシュ用変数 (GC Alloc 削減)
        private IInteractable _currentInteractable;
        private Transform _cameraTransform;
        private RaycastHit _raycastHit; // 構造体なので再利用可能

        // Input System 用
        private InputAction _interactActionInstance;

        private void Awake()
        {
            // カメラ参照の解決
            _cameraTransform = _playerCamera ? _playerCamera.transform : Camera.main?.transform;
            if (_cameraTransform == null)
            {
                Debug.LogError("[PlayerInteractor] カメラが見つかりません。_playerCamera を設定するか、MainCamera タグを持つカメラを配置してください。", this);
            }

            // InputActionReference から実体を取得・有効化
            if (_interactAction != null)
            {
                _interactActionInstance = _interactAction.action;
                _interactActionInstance.Enable();
                _interactActionInstance.performed += OnInteractPerformed;
            }
            else
            {
                Debug.LogError("[PlayerInteractor] Interact Action Reference が未設定です。", this);
            }
        }

        private void OnDestroy()
        {
            // イベント購読解除と無効化
            if (_interactActionInstance != null)
            {
                _interactActionInstance.performed -= OnInteractPerformed;
                _interactActionInstance.Disable();
            }
        }

        private void Update()
        {
            // 毎フレーム Raycast を実行し、現在見ているインタラクト対象を更新する
            // (入力とは非同期で視線先のハイライト/テキスト更新を行うため)
            PerformRaycast();
        }

        /// <summary>
        /// カメラ正面に Raycast を飛ばし、IInteractable を検索する
        /// TryGetComponent を使用し、GC Alloc を発生させない
        /// </summary>
        private void PerformRaycast()
        {
            if (_cameraTransform == null) return;

            // Raycast 実行 (LayerMask で無駄な衝突計算をカット)
            bool hit = Physics.Raycast(
                _cameraTransform.position,
                _cameraTransform.forward,
                out _raycastHit, // 構造体 out パラメータ (アロケーションなし)
                _interactDistance,
                _interactableLayerMask,
                QueryTriggerInteraction.Collide // Trigger も検知する場合
            );

            // デバッグ描画
            if (_showDebugRay)
            {
                Debug.DrawRay(_cameraTransform.position, _cameraTransform.forward * _interactDistance, hit ? _hitColor : _missColor);
            }

            if (hit)
            {
                // ヒットしたオブジェクト (または親) から IInteractable を取得
                // TryGetComponent は失敗しても GC Alloc が発生しない
                if (_raycastHit.transform.TryGetComponent(out IInteractable interactable)
                    || _raycastHit.transform.GetComponentInParent<IInteractable>() is IInteractable parentInteractable) // 親検索は TryGetComponent できないためパターンマッチングで null チェック
                {
                    // 親から見つかった場合はそれを優先
                    IInteractable found = interactable ?? parentInteractable;

                    // 対象が変わった時のみ更新 (無駄な代入・UI更新を防ぐ)
                    if (_currentInteractable != found)
                    {
                        _currentInteractable = found;
                        OnTargetChanged?.Invoke(_currentInteractable);
                    }
                    return;
                }
            }

            // ヒットしなかった、または IInteractable がない場合
            if (_currentInteractable != null)
            {
                _currentInteractable = null;
                OnTargetChanged?.Invoke(null);
            }
        }

        /// <summary>
        /// 入力アクション発火時のコールバック (イベント駆動)
        /// </summary>
        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            // 現在有効なターゲットがいれば実行
            if (_currentInteractable != null)
            {
                _currentInteractable.Interact(gameObject);
                OnInteractExecuted?.Invoke(_currentInteractable);
            }
        }

        // --- 外部通知用イベント (UI 等が購読) ---

        /// <summary>注視対象の IInteractable が変化した時に発火 (null の場合は対象なし)</summary>
        public event System.Action<IInteractable> OnTargetChanged;

        /// <summary>インタラクト実行時に発火</summary>
        public event System.Action<IInteractable> OnInteractExecuted;

        // --- Public Getter (UI 等から現在のテキストを取得する場合) ---

        public string CurrentInteractText => _currentInteractable?.GetInteractText();
        public bool HasTarget => _currentInteractable != null;
    }
}
