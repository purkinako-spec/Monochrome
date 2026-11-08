using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Plugins.Localization.Editor
{
    public class DialogueLocalizationWindow : EditorWindow
    {
        private enum Tab
        {
            Export,
            Import,
            Test
        }

        private Tab currentTab = Tab.Export;
        private Vector2 scrollPos;

        // Export settings
        private DialogueContainer targetContainer;
        private bool exportAllContainers = false;
        private string exportFilePath = "Assets/DialogueLocalization.csv";

        // Import settings
        private TextAsset csvFileAsset;
        private string targetLanguageCode = "en";
        private string targetLanguageDisplayName = "English";
        private DialogueTextTable targetTextTable;
        private string targetTableSavePath = "Assets/DialogueSystem/Data/DialogueText_EN.asset";

        [MenuItem("Tools/Dialogue/Localization Manager", false, 100)]
        public static void OpenWindow()
        {
            var window = GetWindow<DialogueLocalizationWindow>("Localization Manager");
            window.minSize = new Vector2(480, 520);
            window.Show();
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying) Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Dialogue System - 多言語管理ツール", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            currentTab = (Tab)GUILayout.Toolbar((int)currentTab, new string[] { "CSVエクスポート", "CSVインポート", "言語テスト" });
            EditorGUILayout.Space(10);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            switch (currentTab)
            {
                case Tab.Export:
                    DrawExportTab();
                    break;
                case Tab.Import:
                    DrawImportTab();
                    break;
                case Tab.Test:
                    DrawTestTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawExportTab()
        {
            EditorGUILayout.LabelField("1. 会話ノードから CSV を書き出す", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("会話データを CSV に書き出します。既に翻訳済みの列（English等）がある場合、その内容を保持したまま新しいセリフを追加（マージ）します。", MessageType.Info);
            EditorGUILayout.Space(8);

            exportAllContainers = EditorGUILayout.Toggle("プロジェクト内すべてを対象", exportAllContainers);

            if (!exportAllContainers)
            {
                targetContainer = (DialogueContainer)EditorGUILayout.ObjectField("対象コンテナ", targetContainer, typeof(DialogueContainer), false);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            exportFilePath = EditorGUILayout.TextField("保存先パス", exportFilePath);
            if (GUILayout.Button("参照...", GUILayout.Width(60)))
            {
                string path = EditorUtility.SaveFilePanel("CSVの保存先", Application.dataPath, "DialogueLocalization.csv", "csv");
                if (!string.IsNullOrEmpty(path))
                {
                    exportFilePath = MakeRelativePath(path);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(14);
            if (GUILayout.Button("CSV をエクスポート", GUILayout.Height(36)))
            {
                ExecuteExport();
            }
        }

        private void DrawImportTab()
        {
            EditorGUILayout.LabelField("2. 翻訳済み CSV を Unity に取り込む", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("翻訳された CSV から指定した言語列を取り込み、DialogueTextTable（テキスト辞書アセット）を自動生成または更新します。", MessageType.Info);
            EditorGUILayout.Space(8);

            csvFileAsset = (TextAsset)EditorGUILayout.ObjectField("翻訳済み CSV ファイル", csvFileAsset, typeof(TextAsset), false);

            EditorGUILayout.Space(4);
            targetLanguageCode = EditorGUILayout.TextField("言語コード (例: en)", targetLanguageCode);
            targetLanguageDisplayName = EditorGUILayout.TextField("表示名 (例: English)", targetLanguageDisplayName);

            EditorGUILayout.Space(4);
            targetTextTable = (DialogueTextTable)EditorGUILayout.ObjectField("出力先 Text Table (任意)", targetTextTable, typeof(DialogueTextTable), false);

            if (targetTextTable == null)
            {
                targetTableSavePath = EditorGUILayout.TextField("新規生成アセットパス", targetTableSavePath);
            }

            EditorGUILayout.Space(14);
            if (GUILayout.Button("CSV をインポート", GUILayout.Height(36)))
            {
                ExecuteImport();
            }
        }

        private void DrawTestTab()
        {
            EditorGUILayout.LabelField("3. 言語切り替えクイックテスト", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("ゲーム実行中に言語をリアルタイムで切り替えて確認できます。", MessageType.Info);
            EditorGUILayout.Space(8);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("言語切り替えテストはゲーム再生中（Play Mode）にのみ動作します。", MessageType.Warning);
                return;
            }

            var mgr = DialogueLocalizationManager.Instance;
            if (mgr == null)
            {
                EditorGUILayout.HelpBox("シーン内に DialogueLocalizationManager が見つかりません。", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField($"現在アクティブな言語: {mgr.CurrentLanguageCode}", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            if (mgr.Config != null)
            {
                foreach (var lang in mgr.Config.SupportedLanguages)
                {
                    if (lang == null) continue;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{lang.displayName} ({lang.languageCode})", GUILayout.Width(200));
                    if (GUILayout.Button("切り替える", GUILayout.Width(100)))
                    {
                        var task = mgr.ChangeLanguageAsync(lang.languageCode);
                        task.ContinueWith(_ => Repaint(), System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        // --- Export 処理 ---
        private void ExecuteExport()
        {
            var containers = new List<DialogueContainer>();
            if (exportAllContainers)
            {
                string[] guids = AssetDatabase.FindAssets("t:DialogueContainer");
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var c = AssetDatabase.LoadAssetAtPath<DialogueContainer>(path);
                    if (c != null) containers.Add(c);
                }
            }
            else
            {
                if (targetContainer != null) containers.Add(targetContainer);
            }

            if (containers.Count == 0)
            {
                EditorUtility.DisplayDialog("エラー", "対象となる DialogueContainer が選択されていません。", "OK");
                return;
            }

            // 既存 CSV の既存列・既存行を読み込んでマージ
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), exportFilePath);
            var existingRows = File.Exists(fullPath) ? DialogueCSVUtility.ParseCSV(File.ReadAllText(fullPath)) : new List<List<string>>();

            // 既存ヘッダーの取得
            var headers = new List<string> { "Key", "Type", "Speaker", "Japanese", "English", "Note" };
            var existingMap = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            if (existingRows.Count > 0)
            {
                var fileHeaders = existingRows[0];
                for (int i = 0; i < fileHeaders.Count; i++)
                {
                    if (!headers.Contains(fileHeaders[i]))
                    {
                        headers.Add(fileHeaders[i]);
                    }
                }

                for (int r = 1; r < existingRows.Count; r++)
                {
                    var row = existingRows[r];
                    if (row.Count == 0) continue;
                    string key = row[0];
                    if (string.IsNullOrEmpty(key)) continue;

                    var colDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int c = 0; c < row.Count && c < fileHeaders.Count; c++)
                    {
                        colDict[fileHeaders[c]] = row[c];
                    }
                    existingMap[key] = colDict;
                }
            }

            // ノード走査
            var outputRows = new List<List<string>> { headers };
            var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var speakers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var container in containers)
            {
                foreach (var node in container.allNodes)
                {
                    if (node == null || string.IsNullOrEmpty(node.nodeID)) continue;

                    // 1. ノード本文行
                    string nodeKey = node.nodeID;
                    if (!processedKeys.Contains(nodeKey))
                    {
                        processedKeys.Add(nodeKey);
                        var row = BuildRow(headers, nodeKey, "Dialogue", node.speakerName, node.dialogueText, existingMap);
                        outputRows.Add(row);
                    }

                    if (!string.IsNullOrEmpty(node.speakerName))
                    {
                        speakers.Add(node.speakerName);
                    }

                    // 2. 選択肢行
                    if (node.choices != null)
                    {
                        for (int i = 0; i < node.choices.Count; i++)
                        {
                            var ch = node.choices[i];
                            if (ch == null) continue;
                            string choiceKey = $"{node.nodeID}#c{i}";
                            if (!processedKeys.Contains(choiceKey))
                            {
                                processedKeys.Add(choiceKey);
                                var chRow = BuildRow(headers, choiceKey, "Choice", "", ch.choiceText, existingMap);
                                outputRows.Add(chRow);
                            }
                        }
                    }
                }
            }

            // 3. 話者名行
            foreach (var sp in speakers)
            {
                string spKey = $"speaker_{sp}";
                if (!processedKeys.Contains(spKey))
                {
                    processedKeys.Add(spKey);
                    var spRow = BuildRow(headers, spKey, "Speaker", sp, sp, existingMap);
                    outputRows.Add(spRow);
                }
            }

            // ディレクトリ作成 & 保存
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string csvContent = DialogueCSVUtility.ToCSV(outputRows);
            DialogueCSVUtility.WriteToFile(fullPath, csvContent);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("完了", $"CSV エクスポートが完了しました！\n出力先: {exportFilePath}\n件数: {outputRows.Count - 1} 行", "OK");
        }

        private List<string> BuildRow(List<string> headers, string key, string type, string speaker, string defaultText, Dictionary<string, Dictionary<string, string>> existingMap)
        {
            var row = new List<string>();
            existingMap.TryGetValue(key, out var existingCols);

            foreach (var h in headers)
            {
                if (h.Equals("Key", StringComparison.OrdinalIgnoreCase)) row.Add(key);
                else if (h.Equals("Type", StringComparison.OrdinalIgnoreCase)) row.Add(type);
                else if (h.Equals("Speaker", StringComparison.OrdinalIgnoreCase)) row.Add(speaker);
                else if (h.Equals("Japanese", StringComparison.OrdinalIgnoreCase)) row.Add(defaultText);
                else
                {
                    // 既存の翻訳やノートがあれば引き継ぐ
                    if (existingCols != null && existingCols.TryGetValue(h, out var val))
                    {
                        row.Add(val);
                    }
                    else
                    {
                        row.Add("");
                    }
                }
            }
            return row;
        }

        // --- Import 処理 ---
        private void ExecuteImport()
        {
            if (csvFileAsset == null)
            {
                EditorUtility.DisplayDialog("エラー", "翻訳済み CSV ファイルを選択してください。", "OK");
                return;
            }

            var rows = DialogueCSVUtility.ParseCSV(csvFileAsset.text);
            if (rows.Count < 2)
            {
                EditorUtility.DisplayDialog("エラー", "CSV にデータが存在しません。", "OK");
                return;
            }

            var headers = rows[0];
            int keyIdx = headers.FindIndex(h => h.Equals("Key", StringComparison.OrdinalIgnoreCase));
            int typeIdx = headers.FindIndex(h => h.Equals("Type", StringComparison.OrdinalIgnoreCase));
            int spkIdx = headers.FindIndex(h => h.Equals("Speaker", StringComparison.OrdinalIgnoreCase));

            // インポート対象の列を探す（指定された言語表示名または言語コード）
            int transIdx = headers.FindIndex(h => h.Equals(targetLanguageDisplayName, StringComparison.OrdinalIgnoreCase) ||
                                                 h.Equals(targetLanguageCode, StringComparison.OrdinalIgnoreCase));

            if (transIdx == -1)
            {
                // 見つからない場合は English 列をフォールバック検索
                transIdx = headers.FindIndex(h => h.Equals("English", StringComparison.OrdinalIgnoreCase));
            }

            if (keyIdx == -1 || transIdx == -1)
            {
                EditorUtility.DisplayDialog("エラー", $"CSV のヘッダーに対象言語の列（'{targetLanguageDisplayName}' または '{targetLanguageCode}'）が見つかりません。", "OK");
                return;
            }

            // 保存先 TextTable アセットの取得または作成
            var table = targetTextTable;
            if (table == null)
            {
                table = AssetDatabase.LoadAssetAtPath<DialogueTextTable>(targetTableSavePath);
                if (table == null)
                {
                    table = ScriptableObject.CreateInstance<DialogueTextTable>();
                    string dir = Path.GetDirectoryName(targetTableSavePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    AssetDatabase.CreateAsset(table, targetTableSavePath);
                }
            }

            table.SetLanguageInfo(targetLanguageCode, targetLanguageDisplayName);
            table.ClearAll();

            int importedCount = 0;
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count <= keyIdx) continue;

                string key = row[keyIdx].Trim();
                if (string.IsNullOrEmpty(key)) continue;

                string trans = row.Count > transIdx ? row[transIdx] : "";
                string type = typeIdx != -1 && row.Count > typeIdx ? row[typeIdx].Trim() : "";
                string spk = spkIdx != -1 && row.Count > spkIdx ? row[spkIdx].Trim() : "";

                if (string.IsNullOrEmpty(trans)) continue; // 翻訳が空欄のものはスキップ（フォールバックさせる）

                if (type.Equals("Speaker", StringComparison.OrdinalIgnoreCase) || key.StartsWith("speaker_", StringComparison.OrdinalIgnoreCase))
                {
                    string realSpkID = key.StartsWith("speaker_") ? key.Substring("speaker_".Length) : (string.IsNullOrEmpty(spk) ? key : spk);
                    table.SetSpeaker(realSpkID, trans);
                }
                else if (type.Equals("Choice", StringComparison.OrdinalIgnoreCase) || key.Contains("#c"))
                {
                    table.SetChoice(key, trans);
                }
                else
                {
                    // Dialogue 本文
                    table.SetEntry(key, spk, trans);
                }

                importedCount++;
            }

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("完了", $"CSV インポートが完了しました！\n言語: {targetLanguageDisplayName} ({targetLanguageCode})\n取り込み件数: {importedCount} 件\n保存先: {AssetDatabase.GetAssetPath(table)}", "OK");
        }

        private string MakeRelativePath(string absolutePath)
        {
            if (absolutePath.StartsWith(Application.dataPath))
            {
                return "Assets" + absolutePath.Substring(Application.dataPath.Length);
            }
            return absolutePath;
        }
    }
}
