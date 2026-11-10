using System;
using System.Collections.Generic;
using UnityEngine;

namespace Monochrome.Core.Input
{
    /// <summary>
    /// 論理キー名（例: "Interact", "Submit", "Cancel"）と現在のアクティブデバイスに応じて、
    /// 適切なボタン表記テキスト（[E], (A), タップなど）やアイコンを解決するユーティリティ。
    /// </summary>
    public static class InputPromptResolver
    {
        // キー表記の簡易定義テーブル (LogicalKey -> Device -> DisplayString)
        private static readonly Dictionary<string, (string pc, string gamepad, string touch)> DefaultKeyBindings =
            new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
            {
                { "Interact", ("[E]", "(A)", "タップ") },
                { "Talk",     ("[E]", "(A)", "タップ") },
                { "Inspect",  ("[E]", "(A)", "タップ") },
                { "Open",     ("[E]", "(A)", "タップ") },
                { "Submit",   ("[Enter]", "(A)", "決定") },
                { "Cancel",   ("[Esc]", "(B)", "戻る") },
                { "Jump",     ("[Space]", "(A)", "ジャンプ") },
                { "Menu",     ("[Tab]", "(Start)", "メニュー") }
            };

        /// <summary>
        /// 論理キーとアクション説明から、現在のデバイスに合わせたプロンプト文字列を生成します。
        /// 例: GetPromptText("Interact", "調べる") -> PCなら "[E] 調べる", Padなら "(A) 調べる"
        /// </summary>
        public static string GetPromptText(string logicalKey, string actionDescription)
        {
            string buttonText = GetButtonText(logicalKey);
            if (string.IsNullOrEmpty(actionDescription))
            {
                return buttonText;
            }

            // スマホタッチの場合は「調べる」のみ、または「タップ: 調べる」
            var device = InputDeviceManager.Instance.CurrentDeviceType;
            if (device == InputDeviceType.Touchscreen)
            {
                return actionDescription;
            }

            return $"{buttonText} {actionDescription}";
        }

        /// <summary>
        /// 論理キーに対応するボタン記号のみを取得します。
        /// </summary>
        public static string GetButtonText(string logicalKey)
        {
            if (string.IsNullOrEmpty(logicalKey)) return string.Empty;

            var device = InputDeviceManager.Instance.CurrentDeviceType;

            if (DefaultKeyBindings.TryGetValue(logicalKey, out var bindings))
            {
                return device switch
                {
                    InputDeviceType.KeyboardMouse => bindings.pc,
                    InputDeviceType.Gamepad => bindings.gamepad,
                    InputDeviceType.Touchscreen => bindings.touch,
                    _ => bindings.pc
                };
            }

            // 未登録キーの場合のフォールバック
            return device switch
            {
                InputDeviceType.Gamepad => "(A)",
                InputDeviceType.Touchscreen => "タップ",
                _ => $"[{logicalKey}]"
            };
        }
    }
}
