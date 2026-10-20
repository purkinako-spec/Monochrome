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
        private Vector2 scrollPos;

        // 💡 上部メニューの Tools > Dialogue > Flag Manager に追加
        [MenuItem("Tools/Dialogue/Flag Manager")]
        public static void ShowWindow()
        {
            GetWindow<FlagManagerWindow>("Flag Manager");
        }

        // ウィンドウを常に更新してリアルタイムな変化を表示する
        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("🎮 Dialogue Flag Manager", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // 実行中（プレイモード）と編集モードで表示を切り替える
            if (Application.isPlaying)
            {
                DrawRuntimeMode();
            }
            else
            {
                DrawEditMode();
            }
        }

        // ==========================================
        // 実行中（リアルタイム監視＆強制書き換え）
        // ==========================================
        private void DrawRuntimeMode()
        {
            EditorGUILayout.HelpBox("現在プレイモードです。リアルタイムでフラグを監視・変更できます。", MessageType.Info);

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("💾 セーブ (Save)", GUILayout.Height(30)))
            {
                FlagManager.Instance.SaveFlags();
            }
            if (GUILayout.Button("📂 ロード (Load)", GUILayout.Height(30)))
            {
                FlagManager.Instance.LoadFlags();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);

            if (FlagManager.Instance == null)
            {
                EditorGUILayout.HelpBox("シーン内に FlagManager が見つかりません。", MessageType.Warning);
                return;
            }

            DrawAddFlagSection(isRuntime: true, null);

            GUILayout.Space(10);
            GUILayout.Label("現在のフラグ一覧 (Runtime)", EditorStyles.boldLabel);

            var flags = FlagManager.Instance.RuntimeFlags;

            if (flags.Count == 0)
            {
                GUILayout.Label("現在設定されているフラグはありません。");
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, "box");

            // 辞書の要素をリスト化して回す（編集中にエラーが出ないようにするため）
            foreach (var key in flags.Keys.ToList())
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(key, GUILayout.Width(150));

                // 値の変更
                int currentValue = flags[key];
                int updatedValue = EditorGUILayout.IntField(currentValue);
                if (currentValue != updatedValue)
                {
                    FlagManager.Instance.SetFlag(key, updatedValue);
                }

                // 削除ボタン
                if (GUILayout.Button("削除", GUILayout.Width(50)))
                {
                    flags.Remove(key);
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

            // シーン内のFlagManagerを探す
            var manager = FindFirstObjectByType<FlagManager>();

            if (manager == null)
            {
                EditorGUILayout.HelpBox("シーン内に FlagManager が配置されていません。\nGameObjectに FlagManager.cs をアタッチしてください。", MessageType.Warning);
                return;
            }

            DrawAddFlagSection(isRuntime: false, manager);

            GUILayout.Space(10);
            GUILayout.Label("ゲーム開始時の初期フラグ (Initial)", EditorStyles.boldLabel);

            if (manager.initialFlags == null || manager.initialFlags.Count == 0)
            {
                GUILayout.Label("初期フラグはありません。");
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, "box");

            for (int i = 0; i < manager.initialFlags.Count; i++)
            {
                var flag = manager.initialFlags[i];
                EditorGUILayout.BeginHorizontal();

                // 初期フラグの場合はキーの名前も変更可能にする
                flag.key = EditorGUILayout.TextField(flag.key, GUILayout.Width(150));
                flag.value = EditorGUILayout.IntField(flag.value);

                if (GUILayout.Button("削除", GUILayout.Width(50)))
                {
                    // 変更を記録（Ctrl+Zで戻せるようにする）
                    Undo.RecordObject(manager, "Delete Flag");
                    manager.initialFlags.RemoveAt(i);
                    // 変更を確定してシーンを保存対象にする
                    EditorUtility.SetDirty(manager);
                    break; // コレクションが変更されたのでループを抜ける
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
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal("box");
            newKey = EditorGUILayout.TextField("New Key", newKey);
            newValue = EditorGUILayout.IntField("Value", newValue);

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
                        // 既に存在するかチェック
                        var existing = manager.initialFlags.Find(f => f.key == newKey);
                        if (existing != null) existing.value = newValue;
                        else manager.initialFlags.Add(new FlagData { key = newKey, value = newValue });
                        EditorUtility.SetDirty(manager);
                    }
                    newKey = ""; // 入力欄をクリア
                    newValue = 0;
                    GUI.FocusControl(null); // フォーカスを外す
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}