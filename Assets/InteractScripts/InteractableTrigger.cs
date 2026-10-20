using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2. トリガー衝突で範囲内の Interactor を管理し、入力通知が来たら自身が対象か判定して実行する
/// NPC/インタラクト対象オブジェクトにアタッチ
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class InteractableTrigger : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("インタラクト可能なレイヤー (Player等)")]
    [SerializeField] private LayerMask interactorLayer;

    // 範囲内にいる Interactor (Player) を管理
    private readonly HashSet<GameObject> _interactorsInside = new HashSet<GameObject>();

    // 同一 GameObject 上の IInteractable 実装をキャッシュ
    private IInteractable[] _interactables;

    private void Awake()
    {
        _interactables = GetComponentsInChildren<IInteractable>(true);
        
        // 自身の Collider を Trigger に強制 (検知用なので)
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // レイヤー判定
        if (((1 << other.gameObject.layer) & interactorLayer) == 0) return;

        _interactorsInside.Add(other.gameObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        _interactorsInside.Remove(other.gameObject);
    }

    /// <summary>
    /// 指定した Interactor が現在範囲内にいるか判定 (PlayerInputDetector から呼ばれる)
    /// </summary>
    public bool IsInteractorInside(GameObject interactor)
    {
        return _interactorsInside.Contains(interactor);
    }

    /// <summary>
    /// 実際のインタラクト処理を実行 (PlayerInputDetector から呼ばれる)
    /// </summary>
    public void ExecuteInteract(GameObject interactor)
    {
        if (_interactables == null || _interactables.Length == 0)
        {
            Debug.LogWarning($"[{name}] IInteractable 実装が見つかりません。", this);
            return;
        }

        foreach (var interactable in _interactables)
        {
            try
            {
                interactable.Interact(interactor);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[{name}] Interact 実行中にエラー: {e.Message}", this);
            }
        }
    }

    // デバッグ用: 範囲内の Interactor を可視化
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        var col = GetComponent<Collider2D>();
        if (col != null) Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}

/// <summary>
/// インタラクト可能なオブジェクトが実装するインターフェース
/// </summary>
public interface IInteractable
{
    void Interact(GameObject interactor);
}
