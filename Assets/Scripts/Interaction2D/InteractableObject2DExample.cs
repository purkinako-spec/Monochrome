using UnityEngine;

namespace InteractionSystem2D
{
    /// <summary>
    /// IInteractable2D のシンプルなサンプル実装（看板、NPC、宝箱等を想定）。
    /// アタッチ時に自動でレイヤーを "Interactable" に設定する。
    /// ※ コライダーの IsTrigger は自動で変更しません（物理衝突が必要なため）。
    /// 検知のみ行いたい場合はインスペクタで手動で IsTrigger を ON にしてください。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class InteractableObject2DExample : MonoBehaviour, IInteractable2D
    {
        [Header("インタラクト設定")]
        [Tooltip("UIに表示されるテキスト（例: 'Eキーで読む', 'Eキーで話す' 等）")]
        [SerializeField] private string interactText = "Eキーでインタラクト";

        [Tooltip("インタラクト可能かどうか（フラグ管理等で動的に切り替え可能）")]
        [SerializeField] private bool isInteractable = true;

        [Header("内部参照")]
        [SerializeField] private Collider2D _collider;

        /// <summary>
        /// エディタでアタッチ/リセット時に自動設定される
        /// </summary>
        private void Reset()
        {
            _collider = GetComponent<Collider2D>();

            // 1. コライダーを Trigger に設定 しない（物理衝突させるため）
            // 必要ならインスペクタで手動でチェックを入れてください。

            // 2. レイヤーを "Interactable" に設定（存在しない場合は警告）
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1)
            {
                gameObject.layer = interactableLayer;
            }
            else
            {
                Debug.LogWarning($"[InteractableObject2DExample] 'Interactable' レイヤーがプロジェクトに存在しません。\n" +
                                 $"Edit > Project Settings > Tags and Layers で Layer 6 などに 'Interactable' を追加してください。", this);
            }
        }

        private void OnValidate()
        {
            // インスペクタ値変更時の自動制御は行わない（物理設定は手動管理推奨）
        }

        // --- IInteractable2D 実装 ---

        public void Interact()
        {
            if (!isInteractable) return;

            // ここに実際の処理を記述（会話開始、アイテム取得、シーン遷移等）
            Debug.Log($"<color=cyan>[Interact]</color> {gameObject.name} がインタラクトされました。", this);

            // 例: 一度きりのインタラクトなら無効化
            // isInteractable = false;
        }

        public string GetInteractText()
        {
            return isInteractable ? interactText : string.Empty;
        }

        /// <summary>
        /// 外部からインタラクト可否を切り替える場合に使用
        /// </summary>
        public void SetInteractable(bool state)
        {
            isInteractable = state;
        }
    }
}
