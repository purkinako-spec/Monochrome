using Runtime.Dialogue;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Editor;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogueSystem.Editor
{
    public class DialogueEditorWindow : EditorWindow
    {
        private DialogueGraphView graphView;
        private DialogueContainer currentContainer;

        private ObjectField containerField;
        private Button loadButton;
        private Button saveButton;
        private Button newButton;
        private Button selectButton;
        private Button debugToggleButton;
        private Button forceSampleButton;
        private Label statusLabel;

        [MenuItem("Tools/Dialogue/Dialogue Editor")]
        public static void OpenDialogueEditor()
        {
            var window = GetWindow<DialogueEditorWindow>("Dialogue Editor");
            window.minSize = new Vector2(600, 400);
        }

        private void OnEnable()
        {
            // 先にツールバーを作ってから GraphView を追加（描画順の確実化）
            ConstructToolbar();
            ConstructGraphView();
            UpdateStatus("Editor ready");
        }

        private void OnDisable()
        {
            if (graphView != null)
            {
                // エラー防止のため Remove() ではなく RemoveFromHierarchy() に変更
                graphView.RemoveFromHierarchy();
                graphView = null;
            }
        }

        private void ConstructGraphView()
        {
            // 1. ツールバーの下の「残りの領域」をすべて埋めるベースコンテナ
            var graphContainer = new VisualElement();
            graphContainer.style.flexGrow = 1;
            graphContainer.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.08f));

            // 2. GraphView を作成
            graphView = new DialogueGraphView
            {
                name = "Dialogue Graph"
            };

            // ベースコンテナいっぱいに広げる
            graphView.StretchToParentSize();

            // 3. コンテナに GraphView を入れ、ルートに追加
            graphContainer.Add(graphView);
            rootVisualElement.Add(graphContainer);
        }

        private void ConstructToolbar()
        {
            // ツールバー（上部固定）
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.height = 30;
            toolbar.style.paddingLeft = 6;
            toolbar.style.paddingRight = 6;
            toolbar.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f));

            // Container ObjectField
            containerField = new ObjectField("Container")
            {
                objectType = typeof(DialogueContainer),
                allowSceneObjects = false,
                style = { flexGrow = 1, minWidth = 200 }
            };
            containerField.RegisterValueChangedCallback(evt =>
            {
                currentContainer = evt.newValue as DialogueContainer;
                UpdateStatus($"Container selected: {currentContainer?.name ?? "null"}");
            });
            toolbar.Add(containerField);

            // Select (ObjectPicker)
            selectButton = new Button(() => EditorGUIUtility.ShowObjectPicker<DialogueContainer>(currentContainer, false, "", 0))
            { text = "Select" };
            toolbar.Add(selectButton);

            // New
            newButton = new Button(() =>
            {
                string path = EditorUtility.SaveFilePanelInProject("Create Dialogue Container", "NewDialogueContainer", "asset", "Create new DialogueContainer asset");
                if (!string.IsNullOrEmpty(path))
                {
                    var asset = CreateInstance<DialogueContainer>();
                    AssetDatabase.CreateAsset(asset, path);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    currentContainer = asset;
                    containerField.value = currentContainer;
                    UpdateStatus("[New] Created: " + path);
                }
            })
            { text = "New" };
            toolbar.Add(newButton);

            // Load
            loadButton = new Button(() =>
            {
                UpdateStatus("[Load] Start");
                try
                {
                    if (currentContainer == null)
                    {
                        string[] guids = AssetDatabase.FindAssets("t:DialogueContainer");
                        if (guids.Length > 0)
                        {
                            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                            currentContainer = AssetDatabase.LoadAssetAtPath<DialogueContainer>(assetPath);
                            containerField.value = currentContainer;
                            UpdateStatus("[Auto] Assigned: " + assetPath);
                        }
                        else
                        {
                            UpdateStatus("[Load] No DialogueContainer asset found");
                            Debug.LogWarning("[DialogueEditor] Load: DialogueContainer が見つかりません。");
                            return;
                        }
                    }

                    // ClearGraph 呼び出し（名前変更済み）
                    graphView.ClearGraph();
                    UpdateStatus("[Load] Cleared graph");

                    Debug.Log("[DialogueEditor] Calling DialogueGraphViewIO.Load...");
                    DialogueGraphViewIO.Load(graphView, currentContainer);

                    int nodeCount = graphView.graphElements.ToList().OfType<DialogueNodeView>().Count();
                    UpdateStatus($"[Load] Completed. Nodes: {nodeCount}");

                    if (nodeCount == 0)
                    {
                        UpdateStatus("[Load] Container empty — creating debug sample node");
                        graphView.CreateNode("Sample Node", new Vector2(100, 100));
                        graphView.FrameAll();
                    }
                    else
                    {
                        graphView.FrameAll();
                    }
                }
                catch (System.Exception ex)
                {
                    UpdateStatus("[Load] Exception: " + ex.Message);
                    Debug.LogError("[DialogueEditor] Load failed: " + ex);
                }
            })
            { text = "Load" };
            toolbar.Add(loadButton);

            // Save
            saveButton = new Button(() =>
            {
                if (currentContainer == null)
                {
                    UpdateStatus("[Save] No container selected");
                    Debug.LogWarning("[DialogueEditor] Save: DialogueContainer が選択されていません。");
                    return;
                }

                try
                {
                    DialogueGraphViewIO.Save(graphView, currentContainer);
#if UNITY_EDITOR
                    EditorUtility.SetDirty(currentContainer);
                    AssetDatabase.SaveAssets();
#endif
                    UpdateStatus("[Save] Saved: " + currentContainer.name);
                }
                catch (System.Exception ex)
                {
                    UpdateStatus("[Save] Exception: " + ex.Message);
                    Debug.LogError("[DialogueEditor] Save failed: " + ex);
                }
            })
            { text = "Save" };
            toolbar.Add(saveButton);

            // ツールメニュー（旧 Handler Info ボタン）
            var toolsMenuButton = new Button(() =>
            {
                GenericMenu menu = new GenericMenu();

                // 1. 既存の Handler Info ウィンドウを開く
                menu.AddItem(new GUIContent("Handler Info"), false, () =>
                {
                    DialogueSystem.Editor.HandlerInfoWindow.ShowWindow();
                });

                // 2. 追加ウィンドウ用のプレースホルダー1
                menu.AddItem(new GUIContent("FlagManagerWindow"), false, () =>
                {
                    FlagManagerWindow.ShowWindow();
                });

                // 3. 追加ウィンドウ用のプレースホルダー2
                menu.AddItem(new GUIContent("PortraitCommandBuilderWindow"), false, () =>
                {
                    PortraitCommandBuilderWindow.ShowWindow();
                });

                // マウスカーソルの位置にドロップダウンを表示
                menu.ShowAsContext();
            })
            { text = "Tools ▾" };
            toolbar.Add(toolsMenuButton);

            // ステータスラベル（右端）
            statusLabel = new Label("Status: ready");
            statusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            statusLabel.style.marginLeft = 8;
            statusLabel.style.minWidth = 220;
            toolbar.Add(statusLabel);

            // ツールバーをルートに追加（最前面）
            rootVisualElement.Add(toolbar);
        }

        private void UpdateStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = $"Status: {message}";
            }
            Debug.Log("[DialogueEditor] " + message);
        }

        private void OnGUI()
        {
            // ObjectPicker の選択確定を受け取る
            if (Event.current.commandName == "ObjectSelectorClosed")
            {
                var picked = EditorGUIUtility.GetObjectPickerObject() as DialogueContainer;
                if (picked != null)
                {
                    currentContainer = picked;
                    if (containerField != null) containerField.value = currentContainer;
                    Repaint();
                    UpdateStatus("[Picker] Selected: " + currentContainer.name);
                }
            }
        }
    }
}