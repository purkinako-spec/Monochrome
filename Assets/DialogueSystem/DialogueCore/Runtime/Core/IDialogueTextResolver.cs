using UnityEngine;

namespace Runtime.Dialogue.Core
{
    /// <summary>
    /// 会話テキスト・話者名・選択肢のローカライズ（多言語解決）を行うインターフェース。
    /// 未設定時はノードに記述されたデフォルトの文字列がそのまま使用されます（完全非破壊）。
    /// Addressables や外部パッケージによる多言語辞書を差し込むための拡張ポイントです。
    /// </summary>
    public interface IDialogueTextResolver
    {
        /// <summary>ノードIDを元にローカライズされた本文を取得します</summary>
        string ResolveText(string nodeID, string defaultText);

        /// <summary>話者ID・名前を元にローカライズされた表示名を取得します</summary>
        string ResolveSpeaker(string speakerID);

        /// <summary>選択肢のローカライズされた文字列を取得します</summary>
        string ResolveChoice(string choiceKey, string defaultChoiceText);

        /// <summary>ノードIDを元にローカライズされたボイスを取得します（任意）</summary>
        AudioClip ResolveVoice(string nodeID, AudioClip defaultVoice = null) => defaultVoice;
    }
}
