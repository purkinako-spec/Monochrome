using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Runtime.Dialogue;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Branching;

namespace DialogueSystem.Editor
{
    /// <summary>
    /// 独自コマンド、分岐ハンドラー、TMPリッチテキストの使用例と説明を表示するエディタウィンドウ
    /// </summary>
    public class HandlerInfoWindow : EditorWindow
    {
        private int selectedTab = 0;
        private readonly string[] tabTitles = { "独自コマンド [ ]", "分岐ハンドラー", "リッチテキスト < >" };
        private Vector2 scrollPosition;

        private struct HandlerData
        {
            public string Name;
            public string Description;
            public string Usage;
        }

        private List<HandlerData> commandInfoList = new List<HandlerData>();
        private List<HandlerData> branchInfoList = new List<HandlerData>();

        [MenuItem("Tools/Dialogue/Handler Info Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<HandlerInfoWindow>("Dialogue Reference");
            window.minSize = new Vector2(500, 400);
            window.RefreshHandlers();
        }

        private void OnEnable()
        {
            RefreshHandlers();
        }

        /// <summary>
        /// [HandlerInfo] 属性が付与されたクラスを自動収集する
        /// </summary>
        public void RefreshHandlers()
        {
            commandInfoList.Clear();
            branchInfoList.Clear();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch { continue; }

                foreach (var type in types)
                {
                    if (type.IsAbstract || type.IsInterface) continue;

                    var attr = type.GetCustomAttribute<HandlerInfoAttribute>();
                    if (attr == null) continue;

                    // コマンドハンドラー (IDialogueCommandHandler) の検出
                    if (typeof(IDialogueCommandHandler).IsAssignableFrom(type))
                    {
                        string cmdName = type.Name;

                        // 👇 MonoBehaviour などの Unityオブジェクト出ない場合のみインスタンス化を試みる
                        if (!typeof(UnityEngine.Object).IsAssignableFrom(type))
                        {
                            try
                            {
                                var instance = Activator.CreateInstance(type) as IDialogueCommandHandler;
                                if (instance != null && !string.IsNullOrEmpty(instance.TargetCommandName))
                                {
                                    cmdName = instance.TargetCommandName;
                                }
                            }
                            catch { }
                        }

                        commandInfoList.Add(new HandlerData
                        {
                            Name = cmdName,
                            Description = attr.Description,
                            Usage = attr.Usage
                        });
                    }
                    // 分岐ハンドラー (IDialogueBranchHandler) の検出
                    else if (typeof(IDialogueBranchHandler).IsAssignableFrom(type))
                    {
                        branchInfoList.Add(new HandlerData
                        {
                            Name = type.Name,
                            Description = attr.Description,
                            Usage = attr.Usage
                        });
                    }
                }
            }
        }

        private void OnGUI()
        {
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            selectedTab = GUILayout.Toolbar(selectedTab, tabTitles, GUILayout.Height(25));
            if (GUILayout.Button("更新", GUILayout.Width(60), GUILayout.Height(25)))
            {
                RefreshHandlers();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            switch (selectedTab)
            {
                case 0:
                    DrawInfoList("登録済み独自コマンド [ ] 一覧", commandInfoList);
                    break;
                case 1:
                    DrawInfoList("登録済み分岐ハンドラー 一覧", branchInfoList);
                    break;
                case 2:
                    DrawRichTextInfo();
                    break;
            }

            GUILayout.EndScrollView();
        }

        private void DrawInfoList(string headerTitle, List<HandlerData> list)
        {
            EditorGUILayout.LabelField(headerTitle, EditorStyles.boldLabel);
            GUILayout.Space(5);

            if (list.Count == 0)
            {
                EditorGUILayout.HelpBox("[HandlerInfo] 属性が付与された対象クラスが見つかりません。", MessageType.Info);
                return;
            }

            foreach (var item in list)
            {
                DrawCardItem(item.Name, item.Usage, item.Description);
            }
        }

        /// <summary>
        /// タブ3: TextMeshPro標準リッチテキストの説明欄
        /// </summary>
        private void DrawRichTextInfo()
        {
            EditorGUILayout.HelpBox(
                "テキスト内に直接記述することで、文字の装飾やフォント変更が可能です。\n" +
                "TextMeshProの標準機能のため、タイピング演出の文字数カウントに影響を与えません。",
                MessageType.Info);

            GUILayout.Space(10);

            DrawCardItem(
                "フォント変更",
                "<font=\"フォント名\">ここだけ変更</font>",
                "指定したフォントに変更します。\n※対象のフォントアセットは必ず「Resources/Fonts & Materials/」直下に配置してください。"
            );

            DrawCardItem(
                "文字色変更",
                "<color=red>赤色</color> または <color=#FF0000>赤色</color>",
                "文字の色を変更します。英語の色名か16進数のカラーコードが使用可能です。"
            );

            DrawCardItem(
                "文字サイズ変更",
                "<size=150%>大きく</size> または <size=40>40px</size>",
                "文字のサイズを変更します。パーセント指定、またはピクセル直接指定が可能です。"
            );

            DrawCardItem(
                "太字 / 斜体",
                "<b>太字</b> / <i>斜体</i>",
                "文字を太字（Bold）、または斜体（Italic）にします。"
            );

            DrawCardItem(
                "縦位置調整 (ルビ等)",
                "<voffset=1em><size=50%>ふりがな</size></voffset>",
                "文字の縦位置（voffset）とサイズを組み合わせて、疑似的にルビなどを振る際に使用します。"
            );
        }

        private void DrawCardItem(string title, string usage, string description)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(usage))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("使用例:");

                // 💡 修正ポイント: 改行数に合わせて描画の高さを動的に計算する
                GUIStyle multiLineStyle = new GUIStyle(EditorStyles.textField)
                {
                    wordWrap = true
                };

                // 改行コード（\n）の数をカウントして行数を取得
                int lineCount = usage.Split('\n').Length;
                // 行数に応じた高さを算出（最低1行分を保証）
                float labelHeight = EditorGUIUtility.singleLineHeight * Mathf.Max(1, lineCount) + 6f;

                EditorGUILayout.SelectableLabel(usage, multiLineStyle, GUILayout.Height(labelHeight));
                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(description))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("説明:");
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }
    }
}