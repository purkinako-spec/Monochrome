using System;
using UnityEngine;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

namespace Runtime.Dialogue.Plugins.Commands
{
    [HandlerInfo(
        description: @"セリフ表示中のフォントスタイルを一時的（または永続的）に切り替える演出コマンドです。
CharacterProfile に設定されたキャラクター固有フォントや、多言語フォントデータベースと連携して動作します。",
        usage: @"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
【基本構文】
[font:style=スタイル名]
[font:style=スタイル名, keep=true]
[font:reset]

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
【パラメータ一覧】
・style : 【必須】切り替えるフォントスタイル名（DialogueFontDatabase に登録された名前）。
          例: style=horror, style=robot, style=pixel, style=cute, style=default
          ※第一引数として [font:horror] のように直接書くことも可能です。
・keep  : 【任意】true を指定すると、次のセリフ（ノード）以降もこのフォントを維持します。
          省略時は false（そのセリフ限りで自動的にキャラ標準フォントに復帰）となります。
・reset : [font:reset] と記述すると、即座に話者の標準フォントに戻します。

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
【使用例】
・一時的にホラー演出:
  あいつが… [font:style=horror]後ろにいる…！
  （※次のセリフに進むと自動で通常のフォントに戻ります）
・しばらくロボットフォントを維持:
  [font:style=robot, keep=true]システム起動。オールグリーン。
・標準フォントへの手動復帰:
  [font:reset]ふぅ、元に戻った。"
    )]
    public class FontCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "font";

        [SerializeField, Tooltip("対象の DialogueViewWindow（未設定時は自動検索）")]
        private DialogueViewWindow dialogueView;

        private void Start()
        {
            if (dialogueView == null)
            {
                dialogueView = UnityEngine.Object.FindAnyObjectByType<DialogueViewWindow>();
            }

            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            ApplyFontCommand(command);
            onComplete?.Invoke();
        }

        public void ForceComplete(DialogueCommand command)
        {
            ApplyFontCommand(command);
        }

        private void ApplyFontCommand(DialogueCommand command)
        {
            if (dialogueView == null)
            {
                dialogueView = UnityEngine.Object.FindAnyObjectByType<DialogueViewWindow>();
            }

            if (dialogueView == null)
            {
                Debug.LogWarning("[FontCommandHandler] DialogueViewWindow が見つかりません。");
                return;
            }

            // [font:reset] の判定
            bool isReset = command.Arguments.ContainsKey("reset") ||
                           command.GetString("style", "").Equals("reset", StringComparison.OrdinalIgnoreCase) ||
                           command.GetString("default", "").Equals("reset", StringComparison.OrdinalIgnoreCase);

            if (isReset)
            {
                dialogueView.ResetFont();
                return;
            }

            // スタイル名の取得（[font:style=horror] または [font:horror]）
            string style = command.GetString("style", command.GetString("default", "default"));
            bool keep = command.GetString("keep", "false").Equals("true", StringComparison.OrdinalIgnoreCase) ||
                        command.GetString("persistent", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

            dialogueView.SetFontStyle(style, keep);
        }
    }
}
