using UnityEngine;

namespace InteractionSystem2D
{
    /// <summary>
    /// 2Dインタラクト可能オブジェクトの共通インターフェース。
    /// 論理キー（"Interact", "Talk", "Inspect" 等）ベースで判定を行う。
    /// </summary>
    public interface IInteractable2D
    {
        /// <summary>
        /// このオブジェクトが反応する論理キー名（InputActionMapのアクション名、または独自の論理名）。
        /// 例: "Interact", "Talk", "Inspect", "Cancel"
        /// </summary>
        string RequiredKey { get; }

        /// <summary>
        /// インタラクト実行試行。
        /// </summary>
        /// <param name="key">プレイヤーから送られた論理キー</param>
        /// <returns>このキーを処理した場合 true、そうでなければ false</returns>
        bool TryInteract(string key);

        /// <summary>
        /// UI表示用のプロンプトテキストを取得（例: "[E] 話す", "[F] 調べる"）。
        /// キー名に応じた表示名（"E", "Space" 等）はプレイヤー側で解決して渡す想定だが、ここでは完成形の文字列を返す。
        /// </summary>
        string GetInteractPrompt();
    }
}
