using Runtime.Dialogue;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Editor;
using Runtime.Dialogue.Plugins.Localization;
using Runtime.Dialogue.Plugins.Localization.Editor;
using System.Collections.Generic;
using System.Linq;
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
        private Button autoLayoutButton;
        private ToolbarToggle miniMapToggle;
        private DropdownField languageDropdown;
        private Label statusLabel;

        private string currentPreviewLang = "Original (ja)";
        private string currentHighlightedNodeID = "";

        [MenuItem("Tools/Dialogue/Dialogue Editor")]
        public static void OpenDialogueEditor()
        {
            var window = GetWindow<DialogueEditorWindow>("Dialogue Editor");
            window.minSize = new Vector2(700, 450);
        }

        /// <summary>
        /// Project からアセットをダブルクリックした際に直接コンテナを渡して開く
        /// </summary>
        public static void OpenAndLoadContainer(DialogueContainer container)
        {
            var window = GetWindow<DialogueEditorWindow>("Dialogue Editor");
            window.minSize = new Vector2(700, 450);
            window.SetContainerAndLoad(container);
        }

        private void OnEnable()
        {
            ConstructToolbar();
            ConstructGraphView();
            UpdateStatus("Editor ready");

            Runtime.Dialogue.Logic.DialogueManager.OnNodeStarted += HandleNodeStartedOnPlaymode;
        }

        private void OnDisable()
        {
            Runtime.Dialogue.Logic.DialogueManager.OnNodeStarted -= HandleNodeStartedOnPlaymode;

            if (graphView != null)
            {
                graphView.RemoveFromHierarchy();
                graphView = null;
            }
        }

        private void HandleNodeStartedOnPlaymode(string nodeID, DialogueNode node)
        {
            if (graphView == null) return;

            var nodeViews = graphView.graphElements.ToList().OfType<DialogueNodeView>().ToList();
            foreach (var nv in nodeViews)
            {
                bool isTarget = nv.NodeData.nodeID == nodeID;
                nv.SetPlayingHighlight(isTarget);
            }
            currentHighlightedNodeID = nodeID;
        }

        public void SetContainerAndLoad(DialogueContainer container)
        {
            currentContainer = container;
            if (containerField != null)
            {
                containerField.value = currentContainer;
            }
            LoadCurrentContainer();
        }

        private void ConstructGraphView()
        {
            var graphContainer = new VisualElement();
            graphContainer.style.flexGrow = 1;
            graphContainer.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.08f));

            graphView = new DialogueGraphView
            {
                name = "Dialogue Graph"
            };

            graphView.StretchToParentSize();

            graphContainer.Add(graphView);
            rootVisualElement.Add(graphContainer);
        }

        private void ConstructToolbar()
        {
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
                style = { flexGrow = 1, minWidth = 160 }
            };
            containerField.RegisterValueChangedCallback(evt =>
            {
                currentContainer = evt.newValue as DialogueContainer;
                UpdateStatus($"Container selected: {currentContainer?.name ?? "null"}");
            });
            toolbar.Add(containerField);

            // Select
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
            loadButton = new Button(() => LoadCurrentContainer())
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

            // Auto Layout ボタン
            autoLayoutButton = new Button(() =>
            {
                if (graphView != null)
                {
                    DialogueAutoLayout.Layout(graphView);
                    graphView.FrameAll();
                    UpdateStatus("[Layout] Auto-aligned nodes");
                }
            })
            { text = "Auto Layout" };
            toolbar.Add(autoLayoutButton);

            // MiniMap トグル
            miniMapToggle = new ToolbarToggle() { text = "Map", value = true };
            miniMapToggle.style.marginLeft = 4;
            miniMapToggle.RegisterValueChangedCallback(evt =>
            {
                graphView?.ToggleMiniMap(evt.newValue);
            });
            toolbar.Add(miniMapToggle);

            // 多言語プレビュードロップダウン
            var langChoices = GetAvailableLanguageOptions();
            languageDropdown = new DropdownField(langChoices, 0);
            languageDropdown.style.width = 110;
            languageDropdown.style.marginLeft = 4;
            languageDropdown.RegisterValueChangedCallback(evt =>
            {
                currentPreviewLang = evt.newValue;
                ApplyLanguagePreview(currentPreviewLang);
            });
            toolbar.Add(languageDropdown);

            // ツールメニュー
            var toolsMenuButton = new Button(() =>
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Container Manager"), false, () => DialogueContainerWindow.OpenWindow());
                menu.AddItem(new GUIContent("Localization Window"), false, () => DialogueLocalizationWindow.OpenWindow());
                menu.AddItem(new GUIContent("Flag Manager"), false, () => FlagManagerWindow.ShowWindow());
                menu.AddItem(new GUIContent("Handler Info"), false, () => HandlerInfoWindow.ShowWindow());
                menu.AddItem(new GUIContent("Portrait Command Builder"), false, () => PortraitCommandBuilderWindow.ShowWindow());
                menu.ShowAsContext();
            })
            { text = "Tools ▾" };
            toolbar.Add(toolsMenuButton);

            // ステータスラベル
            statusLabel = new Label("Status: ready");
            statusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            statusLabel.style.marginLeft = 8;
            statusLabel.style.minWidth = 150;
            toolbar.Add(statusLabel);

            rootVisualElement.Add(toolbar);
        }

        private void LoadCurrentContainer()
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

                graphView.ClearGraph();
                DialogueGraphViewIO.Load(graphView, currentContainer);

                int nodeCount = graphView.graphElements.ToList().OfType<DialogueNodeView>().Count();
                UpdateStatus($"[Load] Completed. Nodes: {nodeCount}");

                if (nodeCount == 0)
                {
                    graphView.CreateNode("Sample Node", new Vector2(100, 100));
                }

                graphView.FrameAll();

                // 現在選択されているプレビュー言語を反映
                ApplyLanguagePreview(currentPreviewLang);
            }
            catch (System.Exception ex)
            {
                UpdateStatus("[Load] Exception: " + ex.Message);
                Debug.LogError("[DialogueEditor] Load failed: " + ex);
            }
        }

        private List<string> GetAvailableLanguageOptions()
        {
            List<string> options = new List<string> { "Original (ja)" };

            // プロジェクト内の DialogueTextTable を検索して言語一覧に追加
            string[] guids = AssetDatabase.FindAssets("t:DialogueTextTable");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var table = AssetDatabase.LoadAssetAtPath<DialogueTextTable>(path);
                if (table != null && !string.IsNullOrEmpty(table.LanguageCode))
                {
                    string label = $"{table.LanguageDisplayName} ({table.LanguageCode})";
                    if (!options.Contains(label)) options.Add(label);
                }
            }
            return options;
        }

        private void ApplyLanguagePreview(string selectedOption)
        {
            if (graphView == null) return;

            var nodeViews = graphView.graphElements.ToList().OfType<DialogueNodeView>().ToList();
            if (selectedOption.StartsWith("Original"))
            {
                // 原文モード（編集可能）に戻す
                foreach (var nv in nodeViews)
                {
                    nv.SetPreviewTranslation(null);
                }
                UpdateStatus("[Preview] Original Text");
                return;
            }

            // 言語コードを抽出（例: "English (en)" -> "en"）
            int startIdx = selectedOption.LastIndexOf('(');
            int endIdx = selectedOption.LastIndexOf(')');
            if (startIdx < 0 || endIdx <= startIdx) return;

            string code = selectedOption.Substring(startIdx + 1, endIdx - startIdx - 1);

            // 対象言語の DialogueTextTable を検索
            string[] guids = AssetDatabase.FindAssets("t:DialogueTextTable");
            DialogueTextTable targetTable = null;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var table = AssetDatabase.LoadAssetAtPath<DialogueTextTable>(path);
                if (table != null && table.LanguageCode.Equals(code, System.StringComparison.OrdinalIgnoreCase))
                {
                    targetTable = table;
                    break;
                }
            }

            if (targetTable != null)
            {
                foreach (var nv in nodeViews)
                {
                    if (targetTable.TryGetText(nv.NodeData.nodeID, out string translated))
                    {
                        nv.SetPreviewTranslation(translated);
                    }
                    else
                    {
                        nv.SetPreviewTranslation("[未翻訳 / Missing Translation]");
                    }
                }
                UpdateStatus($"[Preview] Applied: {code}");
            }
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
