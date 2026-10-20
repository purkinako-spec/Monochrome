using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Runtime.Dialogue.Interaction;

namespace Runtime.Dialogue.Interaction
{
    /// <summary>
    /// Script 1: Player にアタッチ
    /// 入力監視・範囲検出・最適な Receiver(Script 2) 選定・通知実行
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Detection Settings")]
        [Tooltip("検出半径")]
        [SerializeField] private float _detectionRadius = 1.5f;
        [Tooltip("インタラクト可能なレイヤー (NPCのレイヤーが含まれているか確認)")]
        [SerializeField] private LayerMask _interactableLayer;
        [Tooltip("検出中心 (未指定なら自身のTransform)")]
        [SerializeField] private Transform _detectionCenter;

        [Header("Input System")]
        [Tooltip("インタラクト入力アクション (例: 'Player/Interact')")]
        [SerializeField] private InputActionReference _interactAction;

        [Header("Debug")]
        [SerializeField] private bool _showGizmos = true;
        [Tooltip("詳細ログを出力する (原因特定用)")]
        [SerializeField] private bool _verboseLogging = true;

        private List<InteractableReceiver> _detectedReceivers = new List<InteractableReceiver>();
        private InteractableReceiver _currentBestTarget;
        private Collider2D[] _overlapBuffer = new Collider2D[20];
        private bool _inputEnabled = false;

        // イベント (UI Presenter が購読)
        public event System.Action<InteractableReceiver> OnTargetChanged;
        public event System.Action OnInteractPerformed;

        // Public Getters
        public InteractableReceiver CurrentTarget => _currentBestTarget;
        public IReadOnlyList<InteractableReceiver> DetectedReceivers => _detectedReceivers;

        private void Awake()
        {
            if (_detectionCenter == null) _detectionCenter = transform;
            ValidateInputAction();
        }

        private void ValidateInputAction()
        {
            if (_interactAction == null || _interactAction.action == null)
            {
                Debug.LogError("[PlayerInteractor] _interactAction が未設定です。InputActionAsset の 'Player/Interact' 等を割り当ててください。", this);
                return;
            }
            try
            {
                _interactAction.action.Enable();
                _interactAction.action.performed += OnInteractInput;
                _inputEnabled = true;
                Debug.Log($"[PlayerInteractor] 入力アクション有効化完了: {_interactAction.action.actionMap?.name}/{_interactAction.action.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerInteractor] 入力アクション有効化失敗: {e.Message}", this);
            }
        }

        private void OnDestroy()
        {
            if (_inputEnabled && _interactAction?.action != null)
            {
                _interactAction.action.performed -= OnInteractInput;
                _interactAction.action.Disable();
            }
        }

        private void Update()
        {
            DetectReceivers();
            UpdateBestTarget();
        }

        /// <summary>
        /// 範囲内の InteractableReceiver (Script 2) を全取得
        /// </summary>
        private void DetectReceivers()
        {
            _detectedReceivers.Clear();
            if (_detectionCenter == null) return;

            int count = Physics2D.OverlapCircleNonAlloc(_detectionCenter.position, _detectionRadius, _overlapBuffer, _interactableLayer);
            
            if (_verboseLogging && count > 0)
            {
                Debug.Log($"[PlayerInteractor] Overlap検出: {count}個 (半径:{_detectionRadius}, レイヤーマスク:{_interactableLayer.value}, 中心:{_detectionCenter.position})");
            }

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null) continue;

                if (_verboseLogging)
                {
                    Debug.Log($"  - Collider: {col.name} (Layer: {LayerMask.LayerToName(col.gameObject.layer)}, IsTrigger: {col.isTrigger})", col);
                }

                // 親も含めて Receiver を探す
                var receiver = col.GetComponentInParent<InteractableReceiver>();
                if (receiver != null)
                {
                    if (!_detectedReceivers.Contains(receiver))
                    {
                        _detectedReceivers.Add(receiver);
                        if (_verboseLogging)
                        {
                            Debug.Log($"    -> InteractableReceiver 発見: {receiver.name} (Processor: {receiver.Processor?.GetType().Name ?? "NULL"})", receiver);
                        }
                    }
                }
                else if (_verboseLogging)
                {
                    Debug.Log($"    -> InteractableReceiver なし (親階層にもなし)", col);
                }
            }
        }

        /// <summary>
        /// 検出リストから「最適なターゲット」を選定 (距離 + 優先度)
        /// </summary>
        private void UpdateBestTarget()
        {
            InteractableReceiver best = null;
            float bestScore = float.MinValue;
            Vector3 centerPos = _detectionCenter.position;

            foreach (var receiver in _detectedReceivers)
            {
                if (receiver == null) 
                {
                    if (_verboseLogging) Debug.Log($"[PlayerInteractor] 評価スキップ: ReceiverがNull");
                    continue; 
                }

                bool canInteract = receiver.CanInteract(gameObject);
                float dist = Vector2.Distance(centerPos, receiver.transform.position);
                float score = receiver.Priority * 1000f - dist;

                if (_verboseLogging)
                {
                    string processorName = receiver.Processor?.GetType().Name ?? "NULL";
                    string containerName = "N/A";
                    if (receiver.Processor is Runtime.Dialogue.Interaction.Processors.DialogueProcessor dp)
                    {
                        containerName = dp.DialogueContainer ? dp.DialogueContainer.name : "NULL";
                    }
                    
                    Debug.Log($"[PlayerInteractor] 評価: {receiver.name} | CanInteract: {canInteract} | Priority: {receiver.Priority} | Dist: {dist:F2} | Score: {score:F2} | Processor: {processorName} | Container: {containerName}");
                }

                if (!canInteract) continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = receiver;
                }
            }

            if (_currentBestTarget != best)
            {
                _currentBestTarget = best;
                if (_verboseLogging)
                {
                    if (best != null)
                        Debug.Log($"[PlayerInteractor] ターゲット決定: {best.name} (Score: {bestScore:F2})", best);
                    else
                        Debug.Log($"[PlayerInteractor] ターゲットクリア (有効な対象なし)");
                }
                OnTargetChanged?.Invoke(_currentBestTarget);
            }
        }

        /// <summary>
        /// 入力検知 -> 現在のターゲット(Script 2) に実行命令
        /// </summary>
        private void OnInteractInput(InputAction.CallbackContext ctx)
        {
            if (_currentBestTarget != null)
            {
                Debug.Log($"[PlayerInteractor] インタラクト実行: {_currentBestTarget.name} ({_currentBestTarget.Processor.GetType().Name})");
                _currentBestTarget.Interact(gameObject); // Script 2 経由で Script 3 へ
                OnInteractPerformed?.Invoke();
            }
            else
            {
                // ここに来る場合、検出0件 または 全てCanInteract=false
                Debug.LogWarning("[PlayerInteractor] インタラクト対象なし (検出0件 または 全てCanInteract=false)。上部の詳細ログを確認してください。");
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showGizmos) return;
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Vector3 center = _detectionCenter ? _detectionCenter.position : transform.position;
            Gizmos.DrawWireSphere(center, _detectionRadius);
            
            if (_currentBestTarget != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(center, _currentBestTarget.transform.position);
            }
        }
    }
}
