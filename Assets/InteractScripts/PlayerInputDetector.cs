using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 1. プレイヤーの入力を監視し、範囲内の InteractableTrigger に通知を送るスクリプト
/// Player にアタッチして使用する
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerInputDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float detectionRadius = 1.5f;
    [SerializeField] private LayerMask interactableLayer;
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
        // 範囲内の Collider を検出
        int count = Physics2D.OverlapCircleNonAlloc(detectionCenter.position, detectionRadius, _overlapBuffer, interactableLayer);
        
        float minDist = float.MaxValue;
        InteractableTrigger bestTarget = null;

        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuffer[i];
            if (col == null) continue;

            // 親階層も含めて InteractableTrigger を探す
            var trigger = col.GetComponentInParent<InteractableTrigger>();
            if (trigger != null)
            {
                float dist = Vector2.Distance(detectionCenter.position, trigger.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestTarget = trigger;
                }
            }
        }
        // 最も近い対象がいれば通知
        if (bestTarget != null)
        {
            bestTarget.OnInteractRequested(gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Vector3 center = detectionCenter ? detectionCenter.position : transform.position;
        Gizmos.DrawWireSphere(center, detectionRadius);
    }
}
