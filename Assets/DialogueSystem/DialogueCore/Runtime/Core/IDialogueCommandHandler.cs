using System;

namespace Runtime.Dialogue
{
    /// <summary>
    /// 演出コマンドを処理するクラスが必ず実装すべきルール
    /// </summary>
    public interface IDialogueCommandHandler
    {
        /// <summary>このクラスが担当するコマンド名（例: "fade_out", "se"）</summary>
        string TargetCommandName { get; }

        /// <summary>通常時の演出実行。終わったら onComplete() を呼ぶこと</summary>
        void Execute(DialogueCommand command, Action onComplete);

        /// <summary>プレイヤーがスキップした時の強制終了処理（一瞬で終わらせる）</summary>
        void ForceComplete(DialogueCommand command);
    }
}