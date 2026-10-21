using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 1. プレイヤーのトリガー判定で範囲内の InteractableTrigger を常時リスト管理し、
/// 入力時に最適な対象 (距離・優先度) を実行する
/// Player にアタッチ (検知用の Collider2D は IsTrigger=true にする)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerInputDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("インタラクト可能なレイヤー (InteractableTrigger がいるレイヤー)")]
    [SerializeField] private LayerMask triggerLayer;
    [Tooltip("検知中心 (未指定なら自身のTransform)")]
    [SerializeField] private Transform detectionCenter;

    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;

    [Header("Debug")]
    [SerializeField] private bool _verboseLogging = false;

    // 範囲内にいる有効な InteractableTrigger を管理
    private readonly HashSet<InteractableTrigger> _triggersInRange = new HashSet<InteractableTrigger>();

    private void Awake()
    {
        if (detectionCenter == null) detectionCenter = transform;

        // 自身の Collider を Trigger に強制 (検知用なので)
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger) col.isTrigger = true;

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

    // --- 物理判定 (継続的な検知) ---

    private void OnTriggerEnter2D(Collider2D other)
    {
        // レイヤー判定
        if (((1 << other.gameObject.layer) & triggerLayer) == 0) return;

        var trigger = other.GetComponentInParent<InteractableTrigger>();
        if (trigger != null)
        {
            _triggersInRange.Add(trigger);
            if (_verboseLogging) Debug.Log($"[PlayerInputDetector] 範囲内検知: {trigger.name}", trigger);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var trigger = other.GetComponentInParent<InteractableTrigger>();
        if (trigger != null)
        {
            _triggersInRange.Remove(trigger);
            if (_verboseLogging) Debug.Log($"[PlayerInputDetector] 範囲外離脱: {trigger.name}", trigger);
        }
    }

    // --- 入力処理 ---

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        InteractableTrigger bestTarget = null;
        float bestScore = float.MinValue;
        Vector3 centerPos = detectionCenter.position;

        // リストから最適なターゲットを選定 (距離 + 優先度)
        foreach (var trigger in _triggersInRange)
        {
            if (trigger == null) continue; // MissingReference 対策

            // NPC側の判定: 「自分が相手の範囲内にいるか」 (相互確認)
            if (!trigger.IsInteractorInside(gameObject)) continue;

            float dist = Vector2.Distance(centerPos, trigger.transform.position);
            int priority = 0;
            // IInteractable から優先度を取得できればする (インターフェース拡張時用)
            // 今回は InteractableTrigger に Priority プロパティを持たせるか、距離のみで判定
            // ここでは距離のみ (スコア = -距離) で最近接を優先
            float score = -dist; 

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = trigger;
            }
        }

        if (bestTarget != null)
        {
            if (_verboseLogging) Debug.Log($"[PlayerInputDetector] インタラクト実行: {bestTarget.name} (Score: {bestScore:F2})");
            bestTarget.ExecuteInteract(gameObject);
        }
        else
        {
            if (_verboseLogging) Debug.Log("[PlayerInputDetector] 有効なインタラクト対象なし (リスト空 or 相互判定不一致)");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        var col = GetComponent<Collider2D>();
        if (col != null) Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
