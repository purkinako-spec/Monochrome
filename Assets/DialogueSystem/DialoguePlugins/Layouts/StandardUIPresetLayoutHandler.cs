using System.Collections.Generic;
using UnityEngine;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

namespace Runtime.Dialogue.Plugins.Layouts
{
    public class StandardUIPresetLayoutHandler : MonoBehaviour, IDialogueLayoutHandler
    {
        // 優先度。複数のハンドラーがある場合、数値が大きいものが先に処理されます
        public int Priority => 0;

        [Header("レイアウトパターンの設定")]
        public List<DialogueLayoutPreset> presets = new List<DialogueLayoutPreset>();

        [Header("UI References (操作対象のUI)")]
        public GameObject backgroundBox;      // テキストウィンドウの背景画像
        public GameObject nameBox;            // 名前表示用の枠
        public RectTransform textRectTransform; // タイピングされるテキストのRectTransform

        [Header("Optional Modules")]
        public GameObject portraitRoot;       // 立ち絵をまとめている親オブジェクト（非表示化用）

        private void Start()
        {
            // ディスパッチャーへ自分自身を登録
            if (DialogueLayoutDispatcher.Instance != null)
            {
                DialogueLayoutDispatcher.Instance.RegisterHandler(this);
            }
        }

        public bool TryHandleLayout(string layoutName, Dictionary<string, string> args)
        {
            var preset = presets.Find(p => p.layoutName == layoutName);

            // 該当するプリセットがない場合は別のハンドラーに処理を譲る
            if (preset == null) return false;

            // UIの表示/非表示の切り替え
            if (backgroundBox != null) backgroundBox.SetActive(preset.showBackgroundBox);
            if (nameBox != null) nameBox.SetActive(preset.showNameBox);

            // 立ち絵レイヤーの一括非表示/再表示
            if (portraitRoot != null)
            {
                portraitRoot.SetActive(!preset.hidePortraits);
            }

            // テキスト領域の変更（ナレーション時などは画面全体に広げる等）
            if (preset.overrideTextRect && textRectTransform != null)
            {
                textRectTransform.offsetMin = preset.textOffsetMin;
                textRectTransform.offsetMax = preset.textOffsetMax;
            }

            return true;
        }
    }
}