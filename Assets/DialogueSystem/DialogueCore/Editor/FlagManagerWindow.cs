using System;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Runtime.Dialogue.Logic;

namespace Runtime.Dialogue.Editor
{
    public class FlagManagerWindow : EditorWindow
    {
        private string newKey = "";
        private int newValue = 0;
        private bool isBoolMode = false;
        private string searchQuery = "";
        private Vector2 scrollPos;

        // 💡 上部メニューの Tools > Dialogue > Flag Manager に追加
        [MenuItem("Tools/Dialogue/Flag Manager")]
        public static void ShowWindow()
        {
            var win = GetWindow<FlagManagerWindow>("Flag Manager");
            win.minSize = new Vector2(350, 400);
        }

        private void OnInspectorUpdate()
        {
            // プレイモード中はフラグがリアルタイムで変わるため再描画
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("🎮 Dialogue Flag Manager", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // 検索バー
            DrawSearchBar();

            if (Application.isPlaying)
            {
                DrawRuntimeMode();
            }
            else
            {
                DrawEditMode();
            }
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("🔍", GUILayout.Width(20));
            searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22)))
            {
                searchQuery = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        // ==========================================
        // 実行中（リアルタイム監視＆強制書き換え）
        // ==========================================
        private void DrawRuntimeMode()
        {
            EditorGUILayout.HelpBox("現在プレイモードです。リアルタイムでフラグを監視・テスト変更できます。", MessageType.Info);

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("💾 セーブ", GUILayout.Height(26)))
            {
                FlagManager.Instance.SaveFlags();
            }
            if (GUILayout.Button("📂 ロード", GUILayout.Height(26)))
            {
                FlagManager.Instance.LoadFlags();
            }
            if (GUILayout.Button("🧹 tmp_ 破棄", GUILayout.Height(26), GUILayout.Width(90)))
            {
                int count = FlagManager.Instance.RemoveFlagsWithPrefix("tmp_");
                Debug.Log($"[FlagManager] 一時フラグ {count} 件を破棄しました");
            }
            if (GUILayout.Button("🔄 初期化", GUILayout.Height(26), GUILayout.Width(70)))
            {
                if (EditorUtility.DisplayDialog("初期化確認", "フラグを初期状態に戻しますか？", "リセット", "キャンセル"))
                {
                    FlagManager.Instance.ResetToInitialFlags();
                }
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            if (FlagManager.Instance == null)
            {
                EditorGUILayout.HelpBox("シーン内に FlagManager が見つかりません。", MessageType.Warning);
                return;
            }

            DrawAddFlagSection(isRuntime: true, null);

            GUILayout.Space(10);
            GUILayout.Label($"現在のフラグ一覧 ({FlagManager.Instance.RuntimeFlags.Count} 件)", EditorStyles.boldLabel);

            var flags = FlagManager.Instance.RuntimeFlags;
            var keys = flags.Keys.ToList();

            // 検索絞り込み
            if (!string.IsNullOrEmpty(searchQuery))
            {
                keys = keys.Where(k => k.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }

            if (keys.Count == 0)
            {
                GUILayout.Label("表示可能なフラグはありません。");
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, "box");

            foreach (var key in keys)
            {
                int currentValue = flags[key];

                EditorGUILayout.BeginHorizontal();

                // キー名
                EditorGUILayout.LabelField(key, EditorStyles.boldLabel, GUILayout.Width(160));

                // トグル / 数値
                bool isBoolLike = (currentValue == 0 || currentValue == 1);
                if (isBoolLike)
                {
                    bool b = currentValue != 0;
                    bool newB = EditorGUILayout.Toggle(b, GUILayout.Width(25));
                    if (newB != b)
                    {
                        FlagManager.Instance.SetBool(key, newB);
                    }
                }

                // 数値フィールド
                int updatedValue = EditorGUILayout.IntField(currentValue, GUILayout.Width(60));
                if (currentValue != updatedValue)
                {
                    FlagManager.Instance.SetFlag(key, updatedValue);
                }

                // カウント +/-
                if (GUILayout.Button("-", GUILayout.Width(22)))
                {
                    FlagManager.Instance.AddFlag(key, -1);
                }
                if (GUILayout.Button("+", GUILayout.Width(22)))
                {
                    FlagManager.Instance.AddFlag(key, 1);
                }

                // 削除
                if (GUILayout.Button("✕", GUILayout.Width(25)))
                {
                    FlagManager.Instance.RemoveFlag(key);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        // ==========================================
        // 編集時（初期フラグの設定）
        // ==========================================
        private void DrawEditMode()
        {
            EditorGUILayout.HelpBox("現在エディタモードです。ゲーム開始時の初期フラグを設定できます。", MessageType.Info);

            var manager = FindFirstObjectByType<FlagManager>();
            if (manager == null)
            {
                EditorGUILayout.HelpBox("シーン内に FlagManager が配置されていません。\nGameObject に FlagManager.cs をアタッチしてください。", MessageType.Warning);
                return;
            }

            DrawAddFlagSection(isRuntime: false, manager);

            GUILayout.Space(10);
            GUILayout.Label($"ゲーム開始時の初期フラグ ({(manager.initialFlags?.Count ?? 0)} 件)", EditorStyles.boldLabel);

            if (manager.initialFlags == null || manager.initialFlags.Count == 0)
            {
                GUILayout.Label("初期フラグはありません。");
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, "box");

            for (int i = 0; i < manager.initialFlags.Count; i++)
            {
                var flag = manager.initialFlags[i];

                if (!string.IsNullOrEmpty(searchQuery) &&
                    flag.key.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();

                flag.key = EditorGUILayout.TextField(flag.key, GUILayout.Width(160));

                bool isBoolLike = (flag.value == 0 || flag.value == 1);
                if (isBoolLike)
                {
                    bool b = flag.value != 0;
                    bool newB = EditorGUILayout.Toggle(b, GUILayout.Width(25));
                    if (newB != b)
                    {
                        Undo.RecordObject(manager, "Toggle Initial Flag");
                        flag.value = newB ? 1 : 0;
                        EditorUtility.SetDirty(manager);
                    }
                }

                int newVal = EditorGUILayout.IntField(flag.value, GUILayout.Width(60));
                if (newVal != flag.value)
                {
                    Undo.RecordObject(manager, "Change Initial Flag Value");
                    flag.value = newVal;
                    EditorUtility.SetDirty(manager);
                }

                if (GUILayout.Button("✕", GUILayout.Width(25)))
                {
                    Undo.RecordObject(manager, "Delete Initial Flag");
                    manager.initialFlags.RemoveAt(i);
                    EditorUtility.SetDirty(manager);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        // ==========================================
        // 追加セクションの共通描画
        // ==========================================
        private void DrawAddFlagSection(bool isRuntime, FlagManager manager)
        {
            GUILayout.Space(5);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("➕ 新規フラグ追加", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            newKey = EditorGUILayout.TextField("Key", newKey);

            isBoolMode = GUILayout.Toggle(isBoolMode, "Boolモード", GUILayout.Width(85));

            if (isBoolMode)
            {
                bool b = newValue != 0;
                bool newB = EditorGUILayout.Toggle(b, GUILayout.Width(25));
                newValue = newB ? 1 : 0;
            }
            else
            {
                newValue = EditorGUILayout.IntField("Value", newValue, GUILayout.Width(110));
            }

            if (GUILayout.Button("追加", GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(newKey))
                {
                    if (isRuntime)
                    {
                        FlagManager.Instance.SetFlag(newKey, newValue);
                    }
                    else
                    {
                        Undo.RecordObject(manager, "Add Flag");
                        var existing = manager.initialFlags.Find(f => f.key == newKey);
                        if (existing != null)
                        {
                            existing.value = newValue;
                        }
                        else
                        {
                            manager.initialFlags.Add(new FlagData { key = newKey, value = newValue });
                        }
                        EditorUtility.SetDirty(manager);
                    }

                    newKey = "";
                    newValue = 0;
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }
}
