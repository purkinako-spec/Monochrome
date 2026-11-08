using System;
using UnityEngine;
using Runtime.Dialogue.Audio;
using Runtime.Dialogue.Branching;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;
using Runtime.Dialogue.Plugins.Localization;

namespace Runtime.Dialogue
{
    /// <summary>
    /// 会話システム全体のファサード（統合アクセス窓口）。
    /// 10個のマネージャーに別々にアクセスする代わりに、DialogueMaster 1つから直感的に操作できます。
    /// （例: DialogueMaster.Audio.PlaySE("ok"); DialogueMaster.Flags.SetFlag("x", 1);）
    /// すべてのコア機能は自動生成＆永続化（DontDestroyOnLoad）され、シーン跨ぎにも完全対応します。
    /// </summary>
    public static class DialogueMaster
    {
        /// <summary>
        /// 会話進行・ステート管理・ノード実行のコア
        /// </summary>
        public static DialogueManager Dialogue => DialogueManager.Instance;

        /// <summary>
        /// Addressables / コンテナの非同期ロード・グループ管理
        /// </summary>
        public static DialogueContainerManager Containers => DialogueContainerManager.Instance;

        /// <summary>
        /// 多言語化・フォント・辞書管理
        /// </summary>
        public static DialogueLocalizationManager Localization => DialogueLocalizationManager.Instance;

        /// <summary>
        /// BGM / SE / ボイス・タイピング効果音管理
        /// </summary>
        public static DialogueAudioManager Audio => DialogueAudioManager.Instance;

        /// <summary>
        /// 進行フラグ・変数の管理
        /// </summary>
        public static FlagManager Flags => FlagManager.Instance;

        /// <summary>
        /// 分岐ハンドラーの管理・ディスパッチハブ
        /// </summary>
        public static DialogueBranchDispatcher BranchDispatcher => DialogueBranchDispatcher.Instance;

        /// <summary>
        /// 会話システム全体を事前に一括初期化（ウォームアップ）します。
        /// </summary>
        public static void InitializeAll()
        {
            var _ = Dialogue;
            var __ = Containers;
            var ___ = Localization;
            var ____ = Audio;
            var _____ = Flags;
            var ______ = BranchDispatcher;
            Debug.Log("<color=cyan>[DialogueMaster]</color> 会話システムコアの統合ウォームアップが完了しました。");
        }
    }
}
