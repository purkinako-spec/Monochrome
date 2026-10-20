using UnityEngine;

/// <summary>
/// 2. 通知を受け取り、自分についている IInteractable 実装スクリプトのメソッドを発火するスクリプト
/// インタラクト対象オブジェクト (NPC等) にアタッチして使用する
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class InteractableTrigger : MonoBehaviour
{
    // 同一 GameObject 上の IInteractable 実装をキャッシュ
    private IInteractable[] _interactables;

    private void Awake()
    {
        // 自身と子要素から IInteractable 実装を全取得
        _interactables = GetComponentsInChildren<IInteractable>(true);
    }

    /// <summary>
    /// PlayerInputDetector から呼ばれるエントリーポイント
    /// </summary>
    /// <param name="interactor">インタラクトした側の GameObject (Player等)</param>
    public void OnInteractRequested(GameObject interactor)
    {
        if (_interactables == null || _interactables.Length == 0)
        {
            Debug.LogWarning($"[{name}] IInteractable 実装が見つかりません。", this);
            return;
        }

        // 見つかった全ての IInteractable に対して Interact を呼び出す
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
}

/// <summary>
/// インタラクト可能なオブジェクトが実装するインターフェース
/// これを実装したスクリプト (StartDialogue, OpenShop 等) を同じ GameObject に付ける
/// </summary>
public interface IInteractable
{
    void Interact(GameObject interactor);
}
