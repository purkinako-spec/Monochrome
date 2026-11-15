using UnityEngine;
using UnityEditor;
using Monochrome.Core.Dialogue;

namespace Runtime.Dialogue.Editor
{
    [CustomEditor(typeof(DialogueLauncherUI))]
    public class DialogueLauncherUIEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DialogueLauncherUI launcher = (DialogueLauncherUI)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "DialogueLauncherUI: UIから会話（Dialogue）を実行する常駐マネージャー。\n" +
                "・画面に常時表示される開始ボタン（タッチ／New Input System対応）\n" +
                "・実行時のスクロール選択と会話開始\n" +
                "・開始ボタンの表示/非表示、有効/無効化API対応\n" +
                "・Addressables & 多言語翻訳連携",
                MessageType.Info
            );
            EditorGUILayout.Space(2);
            if (GUILayout.Button("📦 Assets/Prefabs/UI/ に Prefab を生成・更新", GUILayout.Height(30)))
            {
                DialogueLauncherUIPrefabCreator.CreatePrefab();
            }
            EditorGUILayout.Space(4);

            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("🎮 Runtime Controls (Play Mode)", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"Status: {(launcher.IsOpen ? "🟢 Panel OPEN" : "⚪ Panel CLOSED")}");
                    EditorGUILayout.LabelField($"Button Visible: {(launcher.IsLauncherButtonVisible ? "YES" : "NO")}");
                    EditorGUILayout.LabelField($"Button Interactable: {(launcher.IsLauncherButtonInteractable ? "YES" : "NO")}");
                    EditorGUILayout.LabelField($"Available Entries: {launcher.Entries.Count} registered");

                    EditorGUILayout.Space(2);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(launcher.IsOpen ? "Close Panel" : "Open Panel", GUILayout.Height(26)))
                        {
                            launcher.ToggleLauncher();
                        }

                        if (GUILayout.Button(launcher.IsLauncherButtonVisible ? "Hide Button" : "Show Button", GUILayout.Height(26)))
                        {
                            launcher.SetLauncherButtonVisible(!launcher.IsLauncherButtonVisible);
                        }

                        if (GUILayout.Button(launcher.IsLauncherButtonInteractable ? "Disable Button" : "Enable Button", GUILayout.Height(26)))
                        {
                            launcher.SetLauncherButtonInteractable(!launcher.IsLauncherButtonInteractable);
                        }
                    }
                }
                EditorGUILayout.Space(6);
            }

            DrawDefaultInspector();
        }
    }
}
