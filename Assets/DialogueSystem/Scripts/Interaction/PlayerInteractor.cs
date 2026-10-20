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
        private InputAction _cachedAction; // 参照をキャッシュして GC/Null チェックを安定化

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
            // 参照解決
            if (_interactAction == null)
            {
                Debug.LogError("[PlayerInteractor] _interactAction が未設定です (InspectorでInputActionReferenceをアサイン)。", this);
                return;
            }

            _cachedAction = _interactAction.action;
            if (_cachedAction == null)
            {
                Debug.LogError("[PlayerInteractor] InputActionReference が Action を解決できません (Action Asset が未設定、または Action 名が間違っている)。", this);
                return;
            }

            try
            {
                // 多重登録防止: 既に有効なら一度クリーンアップ
                CleanupInputActionInternal();

                // ★ 重要: Action Map / Asset 全体を有効化しないと Native 側でコールバックが来ない、またはクラッシュするケースがある
                if (_cachedAction.actionMap != null && !_cachedAction.actionMap.enabled)
                {
                    _cachedAction.actionMap.Enable();
                }
                else if (_cachedAction.asset != null && !_cachedAction.asset.enabled)
                {
                    _cachedAction.asset.Enable();
                }

                // 個別 Action の有効化 (Map/Asset が有効なら不要だが念のため)
                if (!_cachedAction.enabled)
                {
                    _cachedAction.Enable();
                }

                _cachedAction.performed += OnInteractInput;
                _inputEnabled = true;

                if (_verboseLogging)
                {
                    Debug.Log($"[PlayerInteractor] 入力アクション有効化完了: Map='{_cachedAction.actionMap?.name}', Action='{_cachedAction.name}', Enabled={_cachedAction.enabled}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerInteractor] 入力アクション有効化中に例外: {e.GetType().Name} - {e.Message}", this);
                CleanupInputActionInternal(); // 失敗したらロールバック
            }
        }

        private void OnDestroy()
        {
            CleanupInputActionInternal();
        }

        private void OnDisable()
        {
            // シーン遷移や無効化時にも確実にクリーンアップ
            CleanupInputActionInternal();
        }

        private void CleanupInputActionInternal()
        {
            if (!_inputEnabled || _cachedAction == null) return;

            try
            {
                _cachedAction.performed -= OnInteractInput;
                
                // Action 自体を無効化 (他のコンポーネントが使っていない前提なら)
                // 共有アセットの場合は Disable しない方が安全な場合もあるため、ここでは Unsubscribe のみに留めるのが無難
                // _cachedAction.Disable(); 
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerInteractor] 入力アクションクリーンアップ中に例外 (無視): {e.Message}");
            }
            finally
            {
                _inputEnabled = false;
                _cachedAction = null;
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
                // Unity の擬似 null チェック (Destroy 直後対応)
                if (!receiver) 
                {
                    if (_verboseLogging) Debug.Log($"[PlayerInteractor] 評価スキップ: Receiverが破棄済み/Null");
                    continue; 
                }

                bool canInteract = false;
                try 
                {
                    canInteract = receiver.CanInteract(gameObject);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[PlayerInteractor] CanInteract 呼び出し中に例外: {receiver.name}", e);
                    continue;
                }

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
            // ★ Native クラッシュ完全防御: このコールバック内で一切の例外を出さない
            try
            {
                // Unity 擬似 null チェック
                if (!_currentBestTarget)
                {
                    if (_verboseLogging)
                    {
                        Debug.LogWarning("[PlayerInteractor] インタラクト対象なし (検出0件 または 全てCanInteract=false)。");
                    }
                    return;
                }

                // Processor 存在チェック
                var processor = _currentBestTarget.Processor;
                if (processor == null)
                {
                    Debug.LogError($"[PlayerInteractor] インタラクト実行失敗: {_currentBestTarget.name} の Processor が Null です。", _currentBestTarget);
                    return;
                }

                if (_verboseLogging)
                {
                    Debug.Log($"[PlayerInteractor] インタラクト実行: {_currentBestTarget.name} ({processor.GetType().Name})");
                }
                
                // 実際の処理実行
                _currentBestTarget.Interact(gameObject);
                OnInteractPerformed?.Invoke();
            }
            catch (System.Exception e)
            {
                // 例外をログに出すだけで飲み込む (Native 層に伝播させない)
                // LogException を使うとスタックトレース付きで出る
                Debug.LogException(new System.Exception($"[PlayerInteractor] インタラクト処理中に例外発生 (ネイティブクラッシュ防止のため捕捉)", e), this);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showGizmos) return;
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Vector3 center = _detectionCenter ? _detectionCenter.position : transform.position;
            Gizmos.DrawWireSphere(center, _detectionRadius);
            
            if (_currentBestTarget)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(center, _currentBestTarget.transform.position);
            }
        }
    }
}
