using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Runtime.Dialogue.Plugins.Portraits;

namespace Runtime.Dialogue.Editor
{
    public class PortraitCommandBuilderWindow : EditorWindow
    {
        private StandardUIPortraitHandler handler;

        // モード管理用の列挙型
        private enum BuilderMode { Normal, ClearAll, ClearSingle }
        private BuilderMode currentMode = BuilderMode.Normal;

        private int selectedCharIndex = 0;
        private int selectedPosIndex = 0;

        private string manualExpression = "smile";
        private string customCharacterID = "";
        private string customPosition = "center";

        // トランジション用パラメータ
        private float fadeTime = 0f;
        private float moveTime = 0f;
        private string motionTrigger = "";

        private bool isManualMode = false;

        [MenuItem("Tools/Dialogue/Portrait Command Builder")]
        public static void ShowWindow()
        {
            var window = GetWindow<PortraitCommandBuilderWindow>("Portrait Builder");
            window.minSize = new Vector2(380, 420);
        }

        private void OnEnable()
        {
            handler = FindFirstObjectByType<StandardUIPortraitHandler>();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("🎭 立ち絵コマンド生成ツール", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // 💡 追加: 3つのモードを切り替えるタブボタン
            string[] toolbarStrings = { "🎨 通常表示", "🧹 全消去(All)", "🧹 個別消去(Single)" };
            currentMode = (BuilderMode)GUILayout.Toolbar((int)currentMode, toolbarStrings, GUILayout.Height(30));
            GUILayout.Space(10);

            string finalTarget = "";
            string finalExp = manualExpression;
            string finalPos = "";

            EditorGUILayout.BeginVertical("box");

            if (currentMode == BuilderMode.ClearAll)
            {
                // 全消去モード
                GUILayout.Label("画面上のすべての立ち絵を非表示にします。", EditorStyles.wordWrappedLabel);
                
                // ClearAllでもフェード時間は指定できるようにする
                GUILayout.Space(5);
                EditorGUILayout.LabelField("トランジション設定", EditorStyles.boldLabel);
                fadeTime = EditorGUILayout.FloatField("フェード時間 (秒)", fadeTime);
            }
            else if (currentMode == BuilderMode.ClearSingle)
            {
                // 個別消去モード（位置だけを選択する）
                GUILayout.Label("指定した位置の立ち絵のみを非表示にします。", EditorStyles.wordWrappedLabel);
                GUILayout.Space(5);

                if (handler != null)
                {
                    var posList = handler.portraitSlots.Select(s => s.positionID).ToList();
                    if (posList.Count > 0)
                    {
                        selectedPosIndex = EditorGUILayout.Popup("消去する位置 (pos)", selectedPosIndex, posList.ToArray());
                        finalPos = posList[selectedPosIndex];
                    }
                    else GUILayout.Label("※表示位置(Slot)が登録されていません");
                }
                else
                {
                    customPosition = EditorGUILayout.TextField("消去する位置 (pos)", customPosition);
                    finalPos = customPosition;
                }

                GUILayout.Space(5);
                EditorGUILayout.LabelField("トランジション設定", EditorStyles.boldLabel);
                fadeTime = EditorGUILayout.FloatField("フェード時間 (秒)", fadeTime);
            }
            else
            {
                // 通常モード
                if (handler == null)
                {
                    EditorGUILayout.HelpBox("シーン内に StandardUIPortraitHandler が見つからないため、手動入力モードになります。", MessageType.Warning);
                    isManualMode = true;
                }
                else
                {
                    isManualMode = EditorGUILayout.Toggle("手動入力モード", isManualMode);
                }

                GUILayout.Space(5);

                if (!isManualMode && handler != null)
                {
                    var charaList = handler.profiles.Select(p => p.characterID).ToList();
                    if (charaList.Count > 0)
                    {
                        selectedCharIndex = EditorGUILayout.Popup("キャラクター (target)", selectedCharIndex, charaList.ToArray());
                        finalTarget = charaList[selectedCharIndex];
                    }
                    else GUILayout.Label("※キャラクターが登録されていません");

                    finalExp = EditorGUILayout.TextField("表情 (exp)", finalExp);

                    var posList = handler.portraitSlots.Select(s => s.positionID).ToList();
                    if (posList.Count > 0)
                    {
                        selectedPosIndex = EditorGUILayout.Popup("表示位置 (pos)", selectedPosIndex, posList.ToArray());
                        finalPos = posList[selectedPosIndex];
                    }
                    else GUILayout.Label("※表示位置(Slot)が登録されていません");
                }
                else
                {
                    customCharacterID = EditorGUILayout.TextField("キャラクター (target)", customCharacterID);
                    finalExp = EditorGUILayout.TextField("表情 (exp)", finalExp);
                    customPosition = EditorGUILayout.TextField("表示位置 (pos)", customPosition);
                    finalTarget = customCharacterID;
                    finalPos = customPosition;
                }
                manualExpression = finalExp;

                // トランジション設定
                GUILayout.Space(10);
                EditorGUILayout.LabelField("トランジション設定", EditorStyles.boldLabel);
                fadeTime = EditorGUILayout.FloatField("フェードイン時間 (秒)", fadeTime);
                moveTime = EditorGUILayout.FloatField("移動時間 (秒)", moveTime);
                motionTrigger = EditorGUILayout.TextField("モーショントリガー", motionTrigger);
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(15);

            // ==========================================
            // コマンド生成
            // ==========================================
            string generatedCommand = "";

            switch (currentMode)
            {
                case BuilderMode.Normal:
                    generatedCommand = $"[portrait:target={finalTarget},exp={finalExp},pos={finalPos}";
                    if (fadeTime > 0f) generatedCommand += $",fade={fadeTime}";
                    if (moveTime > 0f) generatedCommand += $",moveTime={moveTime}";
                    if (!string.IsNullOrEmpty(motionTrigger)) generatedCommand += $",motion={motionTrigger}";
                    generatedCommand += "]";
                    break;
                case BuilderMode.ClearAll:
                    generatedCommand = "[portrait:target=clear,pos=all";
                    if (fadeTime > 0f) generatedCommand += $",fade={fadeTime}";
                    generatedCommand += "]";
                    break;
                case BuilderMode.ClearSingle:
                    generatedCommand = $"[portrait:target=clear,pos={finalPos}";
                    if (fadeTime > 0f) generatedCommand += $",fade={fadeTime}";
                    generatedCommand += "]";
                    break;
            }

            GUILayout.Label("生成されたコマンド:", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(generatedCommand, EditorStyles.textField, GUILayout.Height(20));
            GUILayout.Space(5);

            GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
            if (GUILayout.Button("📋 コマンドをクリップボードにコピー", GUILayout.Height(35)))
            {
                bool valid = true;
                if (currentMode == BuilderMode.Normal && (string.IsNullOrEmpty(finalTarget) || string.IsNullOrEmpty(finalPos)))
                {
                    valid = false;
                }
                else if (currentMode == BuilderMode.ClearSingle && string.IsNullOrEmpty(finalPos))
                {
                    valid = false;
                }

                if (!valid)
                {
                    Debug.LogWarning("必要な項目が入力されていません。");
                }
                else
                {
                    GUIUtility.systemCopyBuffer = generatedCommand;
                    Debug.Log($"[コピー完了] {generatedCommand}");
                    this.ShowNotification(new GUIContent("コピーしました！"));
                }
            }
            GUI.backgroundColor = Color.white;
        }
    }
}
