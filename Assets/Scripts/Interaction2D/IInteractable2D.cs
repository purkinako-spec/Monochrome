using UnityEngine;

namespace InteractionSystem2D
{
    /// <summary>
    /// 2Dインタラクト可能オブジェクトの共通インターフェース。
    /// プレイヤー側はこのインターフェースのみを参照し、具体的な実装を知る必要がない。
    /// </summary>
    public interface IInteractable2D
    {
        /// <summary>
        /// インタラクト実行時の処理。
        /// </summary>
        void Interact();

        /// <summary>
        /// UI表示用のテキストを取得（例: "Eキーで話す", "Eキーで調べる" 等）。
        /// </summary>
        /// <returns>表示する文字列</returns>
        string GetInteractText();
    }
}
