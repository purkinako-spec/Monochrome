using UnityEngine;

namespace Runtime.Dialogue.Interaction
{
    /// <summary>
    /// Script 3 基底クラス: 実際のインタラクト処理を行う MonoBehaviour 基底クラス
    /// 各機能（会話、釣り、扉等）はこれを継承して実装する
    /// </summary>
    public abstract class InteractableProcessor : MonoBehaviour
    {
        /// <summary> 実行可否判定（距離以外の条件：クエスト状態、アイテム所持等） </summary>
        public abstract bool CanInteract(GameObject interactor);

        /// <summary> インタラクト実行本体 </summary>
        public abstract void Interact(GameObject interactor);

        /// <summary> UI表示用テキスト（例: "話しかける", "釣りをする", "開ける") </summary>
        public abstract string GetInteractionText();

        /// <summary> 優先度（複数候補時のソート用。高いほど優先） </summary>
        public abstract int Priority { get; }
    }
}
