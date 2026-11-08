using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Runtime.Dialogue.Branching;

namespace DialogueSystem.Editor
{
    [CustomEditor(typeof(KeyNavBranchHandler))]
    public class KeyNavBranchHandlerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var prefabProp = serializedObject.FindProperty("branchUIPrefab");
            GameObject prefab = prefabProp?.objectReferenceValue as GameObject;

            // プレハブの検証セクションを描画
            DrawPrefabValidationBox(prefab);

            // デフォルトの Inspector 各フィールドを描画
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPrefabValidationBox(GameObject prefab)
        {
            if (prefab == null)
            {
                EditorGUILayout.HelpBox("⚠️ Branch UI Prefab が未設定です。選択肢ボタンの親となるプレハブを割り当ててください。", MessageType.Warning);
                return;
            }

            // プレハブ内の Button および Text コンポーネントを検出
            var buttons = prefab.GetComponentsInChildren<Button>(true);
            int buttonCount = buttons.Length;

            if (buttonCount == 0)
            {
                EditorGUILayout.HelpBox("❌ 設定されたプレハブ内に Button コンポーネントが見つかりません！\n選択肢を表示するために少なくとも 1 つ以上の Button を配置してください。", MessageType.Error);
                return;
            }

            int validTextCount = 0;
            foreach (var btn in buttons)
            {
                if (btn.GetComponentInChildren<TextMeshProUGUI>(true) != null || btn.GetComponentInChildren<Text>(true) != null)
                {
                    validTextCount++;
                }
            }

            // 正常または情報ボックス
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = prevBg;

            GUILayout.Label($"✨ Prefab 検証ステータス: 良好", EditorStyles.boldLabel);
            GUILayout.Label($"・検出されたボタン数: {buttonCount} 個（最大 {buttonCount} 選択肢に対応）");

            if (validTextCount < buttonCount)
            {
                EditorGUILayout.HelpBox($"注意: {buttonCount - validTextCount} 個のボタンに Text/TextMeshProUGUI が見つかりませんでした。", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
}
