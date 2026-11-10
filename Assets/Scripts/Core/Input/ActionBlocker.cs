using System;

namespace Monochrome.Core.Input
{
    /// <summary>
    /// プレイヤー操作の禁止要因を表すビットフラグ。
    /// 複数の要因（会話中、エリア移動中、メニュー中など）が重なっても
    /// 安全に管理し、意図せぬ早期解除を防ぐ構造です。
    /// </summary>
    [Flags]
    public enum ActionBlocker
    {
        None        = 0,
        Dialogue    = 1 << 0, // 会話中
        Transition  = 1 << 1, // エリア移動・フェード・ロード中
        Menu        = 1 << 2, // ポーズメニュー・インベントリ等
        Minigame    = 1 << 3, // ミニゲーム中
        Cutscene    = 1 << 4, // カットシーン・イベント自動歩行中
        System      = 1 << 5, // システムローディング・エラーダイアログ等
        Custom      = 1 << 6  // 任意の一時的ロック
    }

    /// <summary>
    /// 操作ブロック状態を保持・判定するコンテナクラス。
    /// プレイヤーや各種システムで保持して状態を問い合わせます。
    /// </summary>
    [Serializable]
    public class ActionBlockState
    {
        private ActionBlocker currentBlockers = ActionBlocker.None;

        /// <summary>
        /// ブロック状態が変化した時に発火するイベント (currentBlockers)
        /// </summary>
        public event Action<ActionBlocker> OnBlockStateChanged;

        /// <summary>
        /// 現在有効になっているすべての禁止要因
        /// </summary>
        public ActionBlocker CurrentBlockers => currentBlockers;

        /// <summary>
        /// 何らかの要因でブロックされているか
        /// </summary>
        public bool IsBlocked => currentBlockers != ActionBlocker.None;

        /// <summary>
        /// 移動可能か（移動を阻害するブロック要因が一切ないか）
        /// </summary>
        public bool CanMove => !HasBlocker(
            ActionBlocker.Dialogue |
            ActionBlocker.Transition |
            ActionBlocker.Menu |
            ActionBlocker.Minigame |
            ActionBlocker.Cutscene |
            ActionBlocker.System);

        /// <summary>
        /// インタラクト可能か（インタラクトを阻害するブロック要因が一切ないか）
        /// </summary>
        public bool CanInteract => !HasBlocker(
            ActionBlocker.Dialogue |
            ActionBlocker.Transition |
            ActionBlocker.Menu |
            ActionBlocker.Minigame |
            ActionBlocker.Cutscene |
            ActionBlocker.System);

        /// <summary>
        /// 禁止要因を追加します
        /// </summary>
        public void AddBlocker(ActionBlocker blocker)
        {
            if (blocker == ActionBlocker.None) return;

            var prev = currentBlockers;
            currentBlockers |= blocker;
            if (prev != currentBlockers)
            {
                OnBlockStateChanged?.Invoke(currentBlockers);
            }
        }

        /// <summary>
        /// 指定した禁止要因を解除します
        /// </summary>
        public void RemoveBlocker(ActionBlocker blocker)
        {
            if (blocker == ActionBlocker.None) return;

            var prev = currentBlockers;
            currentBlockers &= ~blocker;
            if (prev != currentBlockers)
            {
                OnBlockStateChanged?.Invoke(currentBlockers);
            }
        }

        /// <summary>
        /// すべての禁止要因を強制解除します
        /// </summary>
        public void ClearAll()
        {
            if (currentBlockers != ActionBlocker.None)
            {
                currentBlockers = ActionBlocker.None;
                OnBlockStateChanged?.Invoke(currentBlockers);
            }
        }

        /// <summary>
        /// 特定の禁止要因が含まれているか確認します
        /// </summary>
        public bool HasBlocker(ActionBlocker blocker) => (currentBlockers & blocker) != 0;
    }
}
