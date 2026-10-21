namespace InteractionSystem
{
    /// <summary>
    /// インタラクト可能なオブジェクトが実装するインターフェース
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// インタラクト実行時の処理
        /// </summary>
        /// <param name="interactor">インタラクトした側の GameObject (Player等)</param>
        void Interact(GameObject interactor);

        /// <summary>
        /// UI表示用のテキストを取得 (例: "ドアを開ける", "アイテムを拾う")
        /// </summary>
        string GetInteractText();
    }
}
