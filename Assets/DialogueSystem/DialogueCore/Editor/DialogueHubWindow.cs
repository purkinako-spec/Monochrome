using System;
using UnityEngine;
using UnityEditor;
using Runtime.Dialogue.Editor;
using Runtime.Dialogue.Plugins.Localization.Editor;

namespace DialogueSystem.Editor
{
    /// <summary>
    /// 会話システム関連の全エディタツールを1つのウィンドウから管理・起動できるダッシュボード
    /// </summary>
    public class DialogueHubWindow : EditorWindow
    {
        private Vector2 scrollPos;

        [MenuItem("Tools/Dialogue/Dialogue Hub (Dashboard)", false, 0)]
        public static void OpenWindow()
        {
            var win = GetWindow<DialogueHubWindow>("Dialogue Hub");
            win.minSize = new Vector2(480, 420);
            win.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(12);

            // ヘッダーデザイン
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.BeginVertical();
            GUILayout.Label("💬 Dialogue System Hub", EditorStyles.largeLabel != null ? EditorStyles.largeLabel : EditorStyles.boldLabel);
            GUILayout.Label("会話・シナリオ制作に関する統合コントロールパネル", EditorStyles.miniLabel);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // 1. ノード・シナリオ編集
            DrawToolSection(
                "🎨 Dialogue Editor (Node Graph)",
                "ビジュアルノードグラフで会話フロー、分岐、テキストを直感的に作成・編集します。\n※ Project内の DialogueContainer をダブルクリックしても起動できます。",
                "Open Dialogue Editor",
                () => DialogueEditorWindow.OpenDialogueEditor()
            );

            // 2. コンテナ & Addressables 管理
            DrawToolSection(
                "📦 Container Manager",
                "全シナリオコンテナのアドレス、ラベル、ロード状態を一元管理します。\n複数ラベルの付与やシーン毎のロード設定もここで行えます。",
                "Open Container Manager",
                () => DialogueContainerWindow.OpenWindow()
            );

            // 3. 多言語ローカライズ管理
            DrawToolSection(
                "🌐 Localization Manager",
                "日英・多言語辞書（TextTable）の編集、CSVインポート/エクスポートを行います。\n翻訳データの管理やチェックを効率化します。",
                "Open Localization Manager",
                () => DialogueLocalizationWindow.OpenWindow()
            );

            // 4. フラグ・進行状況管理
            DrawToolSection(
                "🚩 Flag Manager",
                "ゲーム開始時の初期フラグ設定や、プレイ中のリアルタイムフラグ監視・強制書き換えを行います。",
                "Open Flag Manager",
                () => FlagManagerWindow.ShowWindow()
            );

            // 5. 分岐ハンドラー & 演出
            DrawToolSection(
                "⚙️ Handlers & Commands Info",
                "プロジェクト内に存在する BranchHandler や立ち絵・演出コマンドの仕様・使い方を確認できます。",
                "Open Handler Info",
                () => HandlerInfoWindow.ShowWindow()
            );

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
        }

        private void DrawToolSection(string title, string description, string buttonText, Action onClick)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Space(4);
            GUILayout.Label(title, EditorStyles.boldLabel);
            GUILayout.Space(2);
            GUILayout.Label(description, EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(buttonText, GUILayout.Width(200), GUILayout.Height(26)))
            {
                onClick?.Invoke();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }
    }
}
