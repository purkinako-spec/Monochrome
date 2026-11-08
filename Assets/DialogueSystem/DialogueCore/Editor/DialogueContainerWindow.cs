using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

#if UNITY_EDITOR
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace Runtime.Dialogue.Editor
{
    public class DialogueContainerWindow : EditorWindow
    {
        private class ContainerItem
        {
            public string guid;
            public string assetPath;
            public string assetName;
            public DialogueContainer container;
            public string addressableName;
            public bool isAddressable;
            public int nodeCount;
            public List<string> labels = new List<string>();
        }

        private Vector2 scrollPos;
        private string searchFilter = "";
        private List<ContainerItem> containerItems = new List<ContainerItem>();
        private bool isLoadedOnlyFilter = false;

        // グループ・ラベル表示用
        private enum ViewMode { AllList, GroupByLabel }
        private ViewMode currentViewMode = ViewMode.AllList;
        private Dictionary<string, bool> labelFoldouts = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        // private string newLabelInput = "";

        [MenuItem("Tools/Dialogue/Container Manager", false, 110)]
        public static void OpenWindow()
        {
            var win = GetWindow<DialogueContainerWindow>("Container Manager");
            win.minSize = new Vector2(750, 480);
            win.Show();
        }

        private void OnEnable()
        {
            RefreshContainerList();
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void RefreshContainerList()
        {
            containerItems.Clear();

            string[] guids = AssetDatabase.FindAssets("t:DialogueContainer");
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var container = AssetDatabase.LoadAssetAtPath<DialogueContainer>(path);
                if (container == null) continue;

                string addr = "";
                bool isAddr = false;
                var labels = new List<string>();

                if (settings != null)
                {
                    var entry = settings.FindAssetEntry(guid);
                    if (entry != null)
                    {
                        isAddr = true;
                        addr = entry.address;
                        labels = entry.labels.ToList();
                    }
                }

                containerItems.Add(new ContainerItem
                {
                    guid = guid,
                    assetPath = path,
                    assetName = Path.GetFileNameWithoutExtension(path),
                    container = container,
                    addressableName = addr,
                    isAddressable = isAddr,
                    nodeCount = container.allNodes != null ? container.allNodes.Count : 0,
                    labels = labels
                });
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            DrawHeader();
            EditorGUILayout.Space(8);
            DrawToolbar();
            EditorGUILayout.Space(6);

            if (currentViewMode == ViewMode.AllList)
            {
                DrawFlatContainerList();
            }
            else
            {
                DrawGroupedContainerList();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Dialogue Container 一元管理ハブ", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("全会話コンテナのメモリ常駐状態（ロード / 未ロード）の監視・手動ロード/アンロード・複数ラベルによるグルーピング管理を行います。", EditorStyles.miniLabel);

            int totalCount = containerItems.Count;
            int loadedCount = 0;

            if (Application.isPlaying && DialogueContainerManager.Instance != null)
            {
                foreach (var item in containerItems)
                {
                    string key = !string.IsNullOrEmpty(item.addressableName) ? item.addressableName : item.assetName;
                    if (DialogueContainerManager.Instance.IsLoaded(key))
                    {
                        loadedCount++;
                    }
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            GUIStyle countStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
            EditorGUILayout.LabelField($"全コンテナ数: {totalCount}", countStyle, GUILayout.Width(130));

            GUI.color = loadedCount > 0 ? Color.green : Color.gray;
            EditorGUILayout.LabelField($"🟢 メモリ常駐中: {loadedCount}", countStyle, GUILayout.Width(150));
            GUI.color = Color.white;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("リスト再スキャン", GUILayout.Width(100)))
            {
                RefreshContainerList();
            }

            if (Application.isPlaying)
            {
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("すべてアンロード", GUILayout.Width(120)))
                {
                    if (DialogueContainerManager.Instance != null)
                    {
                        DialogueContainerManager.Instance.UnloadAll();
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            searchFilter = EditorGUILayout.TextField("検索", searchFilter, GUILayout.Width(250));
            isLoadedOnlyFilter = EditorGUILayout.ToggleLeft("ロード中のみ表示", isLoadedOnlyFilter, GUILayout.Width(120));

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("表示形式:", GUILayout.Width(60));
            currentViewMode = (ViewMode)EditorGUILayout.EnumPopup(currentViewMode, GUILayout.Width(140));

            EditorGUILayout.EndHorizontal();
        }

        // --- フラット一覧表示 ---
        private void DrawFlatContainerList()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // テーブルヘッダー
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("コンテナ名", GUILayout.Width(170));
            GUILayout.Label("Addressable アドレス", GUILayout.Width(150));
            GUILayout.Label("ラベル (複数可)", GUILayout.Width(160));
            GUILayout.Label("ノード数", GUILayout.Width(55));
            GUILayout.Label("メモリ状態", GUILayout.Width(80));
            GUILayout.Label("操作", GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            foreach (var item in containerItems)
            {
                if (!MatchesFilter(item)) continue;

                string key = !string.IsNullOrEmpty(item.addressableName) ? item.addressableName : item.assetName;
                bool isLoaded = Application.isPlaying && DialogueContainerManager.Instance != null && DialogueContainerManager.Instance.IsLoaded(key);

                if (isLoadedOnlyFilter && !isLoaded) continue;

                DrawContainerRow(item, key, isLoaded);
            }

            EditorGUILayout.EndScrollView();
        }

        // --- ラベル別グループ表示 ---
        private void DrawGroupedContainerList()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // 存在するすべてのラベルを収集
            var allLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in containerItems)
            {
                foreach (var l in item.labels) allLabels.Add(l);
            }

            // ラベルなしのグループ用
            var unassignedItems = containerItems.Where(i => i.labels.Count == 0 && MatchesFilter(i)).ToList();

            foreach (var label in allLabels.OrderBy(l => l))
            {
                var matchingItems = containerItems.Where(i => i.labels.Contains(label) && MatchesFilter(i)).ToList();
                if (matchingItems.Count == 0) continue;

                int loadedInGroup = matchingItems.Count(i => Application.isPlaying && DialogueContainerManager.Instance != null &&
                    DialogueContainerManager.Instance.IsLoaded(!string.IsNullOrEmpty(i.addressableName) ? i.addressableName : i.assetName));

                if (!labelFoldouts.ContainsKey(label)) labelFoldouts[label] = true;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                labelFoldouts[label] = EditorGUILayout.Foldout(labelFoldouts[label], $"🏷️ {label} ({matchingItems.Count}個 / 🟢 {loadedInGroup}個 ロード中)", true, EditorStyles.foldoutHeader);

                GUILayout.FlexibleSpace();

                if (Application.isPlaying)
                {
                    GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                    if (GUILayout.Button("一括ロード", GUILayout.Width(80)))
                    {
                        _ = DialogueContainerManager.Instance.LoadByLabelAsync(label);
                    }
                    GUI.backgroundColor = new Color(1f, 0.8f, 0.6f);
                    if (GUILayout.Button("一括解放", GUILayout.Width(80)))
                    {
                        DialogueContainerManager.Instance.UnloadByLabel(label);
                    }
                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndHorizontal();

                if (labelFoldouts[label])
                {
                    foreach (var item in matchingItems)
                    {
                        string key = !string.IsNullOrEmpty(item.addressableName) ? item.addressableName : item.assetName;
                        bool isLoaded = Application.isPlaying && DialogueContainerManager.Instance != null && DialogueContainerManager.Instance.IsLoaded(key);
                        DrawContainerRow(item, key, isLoaded, indent: true);
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            // 未分類（ラベルなし）
            if (unassignedItems.Count > 0)
            {
                if (!labelFoldouts.ContainsKey("(未分類)")) labelFoldouts["(未分類)"] = true;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                labelFoldouts["(未分類)"] = EditorGUILayout.Foldout(labelFoldouts["(未分類)"], $"📁 未分類・ラベルなし ({unassignedItems.Count}個)", true, EditorStyles.foldoutHeader);

                if (labelFoldouts["(未分類)"])
                {
                    foreach (var item in unassignedItems)
                    {
                        string key = !string.IsNullOrEmpty(item.addressableName) ? item.addressableName : item.assetName;
                        bool isLoaded = Application.isPlaying && DialogueContainerManager.Instance != null && DialogueContainerManager.Instance.IsLoaded(key);
                        DrawContainerRow(item, key, isLoaded, indent: true);
                    }
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        private bool MatchesFilter(ContainerItem item)
        {
            if (string.IsNullOrEmpty(searchFilter)) return true;

            if (item.assetName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)) return true;
            if (item.addressableName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)) return true;
            if (item.labels.Any(l => l.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))) return true;

            return false;
        }

        private void DrawContainerRow(ContainerItem item, string key, bool isLoaded, bool indent = false)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            if (indent) GUILayout.Space(15);

            // 1. コンテナ名
            if (GUILayout.Button(new GUIContent(item.assetName, EditorGUIUtility.IconContent("ScriptableObject Icon").image), EditorStyles.label, GUILayout.Width(170)))
            {
                Selection.activeObject = item.container;
                EditorGUIUtility.PingObject(item.container);
            }

            // 2. Addressable アドレス
            if (item.isAddressable)
            {
                EditorGUILayout.LabelField(item.addressableName, GUILayout.Width(150));
            }
            else
            {
                GUI.backgroundColor = new Color(0.8f, 0.9f, 1f);
                if (GUILayout.Button("Addressable化", GUILayout.Width(150)))
                {
                    MakeAssetAddressable(item);
                }
                GUI.backgroundColor = Color.white;
            }

            // 3. ラベル（バッジ表示 ＆ 追加ボタン）
            EditorGUILayout.BeginHorizontal(GUILayout.Width(160));
            if (item.isAddressable)
            {
                string labelText = item.labels.Count > 0 ? string.Join(", ", item.labels) : "なし";
                if (GUILayout.Button(new GUIContent(labelText, "クリックしてラベルを編集"), EditorStyles.miniButton, GUILayout.Width(125)))
                {
                    ShowLabelMenu(item);
                }
                if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(25)))
                {
                    ShowAddLabelDialog(item);
                }
            }
            else
            {
                EditorGUILayout.LabelField("-", GUILayout.Width(150));
            }
            EditorGUILayout.EndHorizontal();

            // 4. ノード数
            EditorGUILayout.LabelField($"{item.nodeCount}", GUILayout.Width(55));

            // 5. メモリ状態バッジ
            if (Application.isPlaying)
            {
                if (isLoaded)
                {
                    GUI.backgroundColor = new Color(0.3f, 1f, 0.3f);
                    GUILayout.Label(" LOADED ", EditorStyles.helpBox, GUILayout.Width(75));
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.contentColor = Color.gray;
                    GUILayout.Label("UNLOADED", EditorStyles.helpBox, GUILayout.Width(75));
                    GUI.contentColor = Color.white;
                }
            }
            else
            {
                GUI.contentColor = Color.gray;
                GUILayout.Label("停止中", EditorStyles.helpBox, GUILayout.Width(75));
                GUI.contentColor = Color.white;
            }

            GUILayout.Space(8);

            // 6. 操作ボタン群
            if (Application.isPlaying)
            {
                if (!isLoaded)
                {
                    GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                    if (GUILayout.Button("ロード", GUILayout.Width(55)))
                    {
                        if (item.isAddressable)
                        {
                            _ = DialogueContainerManager.Instance.LoadContainerAsync(item.addressableName);
                        }
                        else
                        {
                            DialogueContainerManager.Instance.RegisterDirectContainer(item.assetName, item.container);
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = new Color(1f, 0.8f, 0.5f);
                    if (GUILayout.Button("解放", GUILayout.Width(55)))
                    {
                        DialogueContainerManager.Instance.UnloadContainer(key);
                    }
                    GUI.backgroundColor = Color.white;
                }

                if (GUILayout.Button("会話テスト", GUILayout.Width(70)))
                {
                    if (DialogueManager.Instance != null)
                    {
                        DialogueManager.Instance.StartDialogue(item.container, () =>
                        {
                            Debug.Log($"[DialogueContainerManager] テスト会話終了: {item.assetName}");
                        });
                    }
                }
            }
            else
            {
                if (GUILayout.Button("開く", GUILayout.Width(55)))
                {
                    Selection.activeObject = item.container;
                    AssetDatabase.OpenAsset(item.container);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // --- ラベル編集コンテキストメニュー ---
        private void ShowLabelMenu(ContainerItem item)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;

            var entry = settings.FindAssetEntry(item.guid);
            if (entry == null) return;

            var menu = new GenericMenu();

            foreach (var label in settings.GetLabels())
            {
                bool hasLabel = entry.labels.Contains(label);
                menu.AddItem(new GUIContent(label), hasLabel, () =>
                {
                    entry.SetLabel(label, !hasLabel);
                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.LabelAdded, entry, true);
                    AssetDatabase.SaveAssets();
                    RefreshContainerList();
                });
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("新規ラベルを追加..."), false, () =>
            {
                ShowAddLabelDialog(item);
            });

            menu.ShowAsContext();
        }

        private void ShowAddLabelDialog(ContainerItem item)
        {
            string newLabel = EditorInputDialog.Show("ラベルの追加", "新しいラベル名を入力してください (例: TownA, Chapter1, Minigame):", "");
            if (!string.IsNullOrEmpty(newLabel))
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings != null)
                {
                    settings.AddLabel(newLabel);
                    var entry = settings.FindAssetEntry(item.guid);
                    if (entry != null)
                    {
                        entry.SetLabel(newLabel, true);
                        settings.SetDirty(AddressableAssetSettings.ModificationEvent.LabelAdded, entry, true);
                        AssetDatabase.SaveAssets();
                        RefreshContainerList();
                    }
                }
            }
        }

        private void MakeAssetAddressable(ContainerItem item)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                EditorUtility.DisplayDialog("エラー", "Addressable Settings が見つかりません。Window > Asset Management > Addressables > Groups を開いて作成してください。", "OK");
                return;
            }

            var group = settings.DefaultGroup;
            var entry = settings.CreateOrMoveEntry(item.guid, group);
            if (entry != null)
            {
                entry.address = item.assetName;
                item.isAddressable = true;
                item.addressableName = item.assetName;
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
                AssetDatabase.SaveAssets();
                RefreshContainerList();
                Debug.Log($"[DialogueContainerWindow] '{item.assetName}' を Addressables に登録しました");
            }
        }
    }

    /// <summary>
    /// 簡単な文字列入力ポップアップダイアログ
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private string description;
        private string inputText;
        private Action<string> onOk;
        // private bool shouldClose = false;

        public static string Show(string title, string description, string defaultText)
        {
            string result = "";
            var window = CreateInstance<EditorInputDialog>();
            window.titleContent = new GUIContent(title);
            window.description = description;
            window.inputText = defaultText;
            window.onOk = (val) => result = val;
            window.minSize = new Vector2(360, 120);
            window.maxSize = new Vector2(360, 120);
            window.ShowModal();
            return result;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(5);
            GUI.SetNextControlName("InputField");
            inputText = EditorGUILayout.TextField(inputText);
            EditorGUI.FocusTextInControl("InputField");

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("キャンセル", GUILayout.Width(80)))
            {
                Close();
            }

            if (GUILayout.Button("OK", GUILayout.Width(80)) || (Event.current.isKey && Event.current.keyCode == KeyCode.Return))
            {
                onOk?.Invoke(inputText.Trim());
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
