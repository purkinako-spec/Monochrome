using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Runtime.Dialogue.Audio;

namespace DialogueSystem.Editor
{
    [CustomPropertyDrawer(typeof(DialogueAudioKeyAttribute))]
    public class DialogueAudioKeyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var audioAttr = attribute as DialogueAudioKeyAttribute;
            List<string> availableKeys = GetAvailableAudioKeys(audioAttr?.AudioTypeFilter);

            if (availableKeys.Count == 0)
            {
                // DBが見つからない or キーが空の時は通常のテキスト入力
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            // 「None (指定なし)」を先頭に追加
            List<string> options = new List<string> { "(None / Manual Input...)" };
            options.AddRange(availableKeys);

            string currentVal = property.stringValue;
            int selectedIndex = 0;

            if (!string.IsNullOrEmpty(currentVal))
            {
                int found = options.IndexOf(currentVal);
                if (found >= 0)
                {
                    selectedIndex = found;
                }
                else
                {
                    // DB外の手動入力値が存在する場合
                    options.Add(currentVal);
                    selectedIndex = options.Count - 1;
                }
            }

            // ドロップダウンと手動入力ボタンを並べる
            float buttonWidth = 65f;
            Rect popupRect = new Rect(position.x, position.y, position.width - buttonWidth - 4, position.height);
            Rect buttonRect = new Rect(position.x + position.width - buttonWidth, position.y, buttonWidth, position.height);

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(popupRect, label.text, selectedIndex, options.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                if (newIndex == 0)
                {
                    property.stringValue = "";
                }
                else
                {
                    property.stringValue = options[newIndex];
                }
            }

            if (GUI.Button(buttonRect, "Edit", EditorStyles.miniButton))
            {
                // 手動手打ちダイアログ
                AudioKeyManualInputDialog.ShowDialog(property.stringValue, newVal =>
                {
                    property.stringValue = newVal;
                    property.serializedObject.ApplyModifiedProperties();
                });
            }
        }

        private List<string> GetAvailableAudioKeys(Runtime.Dialogue.Audio.AudioType? filter)
        {
            List<string> keys = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:DialogueAudioDatabase");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var db = AssetDatabase.LoadAssetAtPath<DialogueAudioDatabase>(path);
                if (db != null)
                {
                    SerializedObject so = new SerializedObject(db);
                    var listProp = so.FindProperty("audioDataList");
                    if (listProp != null && listProp.isArray)
                    {
                        for (int i = 0; i < listProp.arraySize; i++)
                        {
                            var element = listProp.GetArrayElementAtIndex(i);
                            var keyProp = element.FindPropertyRelative("key");
                            var typeProp = element.FindPropertyRelative("type");

                            if (keyProp != null && !string.IsNullOrEmpty(keyProp.stringValue))
                            {
                                if (filter == null || typeProp == null || typeProp.enumValueIndex == (int)filter.Value)
                                {
                                    if (!keys.Contains(keyProp.stringValue))
                                    {
                                        keys.Add(keyProp.stringValue);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return keys;
        }

        private class AudioKeyManualInputDialog : EditorWindow
        {
            private string currentText;
            private Action<string> onConfirm;

            public static void ShowDialog(string initial, Action<string> onConfirm)
            {
                var win = CreateInstance<AudioKeyManualInputDialog>();
                win.titleContent = new GUIContent("Audio Key Input");
                win.currentText = initial ?? "";
                win.onConfirm = onConfirm;
                win.minSize = new Vector2(300, 100);
                win.maxSize = new Vector2(300, 100);
                win.ShowAuxWindow();
            }

            private void OnGUI()
            {
                EditorGUILayout.Space(10);
                currentText = EditorGUILayout.TextField("Key Name", currentText);
                EditorGUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("OK"))
                {
                    onConfirm?.Invoke(currentText);
                    Close();
                }
                if (GUILayout.Button("Cancel"))
                {
                    Close();
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
