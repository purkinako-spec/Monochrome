using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 1. 入力を監視し、押下時に周囲の InteractableTrigger を物理検索し、
/// 「自分が範囲内にいる(IsInteractorInside)かつ最も近い」ものを実行する
/// Player にアタッチ
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerInputDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("検索半径 (トリガー判定の補助用。これより遠いと無視)")]
    [SerializeField] private float searchRadius = 2f;
    [Tooltip("InteractableTrigger がいるレイヤー")]
    [SerializeField] private LayerMask triggerLayer;
    [SerializeField] private Transform detectionCenter;

    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;

    private Collider2D[] _overlapBuffer = new Collider2D[10];

    private void Awake()
    {
        if (detectionCenter == null) detectionCenter = transform;
        
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += OnInteractPerformed;
        }
        else
        {
            Debug.LogError("[PlayerInputDetector] Interact Action が設定されていません。", this);
        }
    }

    private void OnDestroy()
    {
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.performed -= OnInteractPerformed;
            interactAction.action.Disable();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        // 1. 物理検索で周囲の InteractableTrigger 候補を取得
        int count = Physics2D.OverlapCircleNonAlloc(detectionCenter.position, searchRadius, _overlapBuffer, triggerLayer);
        
        float minDist = float.MaxValue;
        InteractableTrigger bestTarget = null;

        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuffer[i];
            if (col == null) continue;

            var trigger = col.GetComponentInParent<InteractableTrigger>();
            if (trigger == null) continue;

            // 2. NPC側のトリガー判定で「自分が範囲内か」を確認 (ここが核心)
            if (!trigger.IsInteractorInside(gameObject)) continue;

            // 3. 距離で最も近いものを選択
            float dist = Vector2.Distance(detectionCenter.position, trigger.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                bestTarget = trigger;
            }
        }

        // 4. 実行
        if (bestTarget != null)
        {
            bestTarget.ExecuteInteract(gameObject);
        }
        else
        {
            // デバッグ用: 近くにいたが範囲外だった、またはトリガーがなかった
            if (_verboseLogging) Debug.Log("[PlayerInputDetector] 有効なインタラクト対象なし (範囲外 or 見つからず)");
        }
    }

    [Header("Debug")]
    [SerializeField] private bool _verboseLogging = false;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Vector3 center = detectionCenter ? detectionCenter.position : transform.position;
        Gizmos.DrawWireSphere(center, searchRadius);
    }
}
