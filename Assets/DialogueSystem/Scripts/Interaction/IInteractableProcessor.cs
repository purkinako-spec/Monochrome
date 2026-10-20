using UnityEngine;

namespace Runtime.Dialogue.Interaction
{
    /// <summary>
    /// Script 3: 実際のインタラクト処理を行うインターフェース
    /// 各機能（会話、釣り、扉等）はこれを実装する
    /// </summary>
    public interface IInteractableProcessor
    {
        /// <summary> 実行可否判定（距離以外の条件：クエスト状態、アイテム所持等） </summary>
        bool CanInteract(GameObject interactor);

        /// <summary> インタラクト実行本体 </summary>
        void Interact(GameObject interactor);

        /// <summary> UI表示用テキスト（例: "話しかける", "釣りをする", "開ける") </summary>
        string GetInteractionText();

        /// <summary> 優先度（複数候補時のソート用。高いほど優先） </summary>
        int Priority { get; }
    }
}
