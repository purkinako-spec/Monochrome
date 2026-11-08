using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Plugins.Localization
{
    /// <summary>
    /// Unity Addressables を使用して、言語別のテキスト辞書やフォントデータベースを
    /// オンデマンド非同期ロード＆アンロードするスマートローダー。
    /// 【特徴】
    /// 1. ja と en で同じフォントを指定している場合はアンロードせずそのまま維持（無駄な再ロード・チラつきを防止）。
    /// 2. 言語切り替え時に不要になったフォントやテキスト辞書のみを確実にメモリから解放（Release）。
    /// 3. アドレス未設定時は直参照へ自動フォールバックするハイブリッド設計。
    /// </summary>
    public class AddressablesLanguageLoader : ILanguageAssetLoader
    {
        // アドレスごとのロード済みハンドル辞書
        private readonly Dictionary<string, AsyncOperationHandle> loadedHandles = new Dictionary<string, AsyncOperationHandle>(StringComparer.OrdinalIgnoreCase);

        // 現在アクティブに利用しているアセットの参照
        private string activeTextAddress = null;
        private string activeFontAddress = null;

        public async Task<LoadedLanguageAssets> LoadAssetsAsync(LanguageAssetConfig config)
        {
            if (config == null) return null;

            DialogueTextTable textTable = null;
            DialogueFontDatabase fontDB = null;

            string targetTextAddress = config.textTableAddress;
            string targetFontAddress = config.fontDatabaseAddress;

            // --- 1. テキスト辞書 (DialogueTextTable) のスマートロード ---
            if (!string.IsNullOrEmpty(targetTextAddress))
            {
                // すでにロード済みならキャッシュを再利用
                if (loadedHandles.TryGetValue(targetTextAddress, out var existingHandle) && existingHandle.IsValid())
                {
                    textTable = existingHandle.Result as DialogueTextTable;
                }
                else
                {
                    try
                    {
                        var handle = Addressables.LoadAssetAsync<DialogueTextTable>(targetTextAddress);
                        textTable = await handle.Task;
                        loadedHandles[targetTextAddress] = handle;
                        Debug.Log($"[AddressablesLanguageLoader] Addressables から TextTable をロード: '{targetTextAddress}'");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AddressablesLanguageLoader] TextTable ロード失敗 ({targetTextAddress}): {ex.Message}");
                    }
                }
            }

            // 直参照フォールバック
            if (textTable == null && config.textTable != null)
            {
                textTable = config.textTable;
            }

            // --- 2. フォントデータベース (DialogueFontDatabase) のスマートロード ---
            if (!string.IsNullOrEmpty(targetFontAddress))
            {
                // すでに同じフォントがロードされているならアンロードせずそのまま再利用！
                if (loadedHandles.TryGetValue(targetFontAddress, out var existingHandle) && existingHandle.IsValid())
                {
                    fontDB = existingHandle.Result as DialogueFontDatabase;
                    Debug.Log($"[AddressablesLanguageLoader] 同じフォントが指定されているため、キャッシュを維持して再利用します: '{targetFontAddress}'");
                }
                else
                {
                    try
                    {
                        var handle = Addressables.LoadAssetAsync<DialogueFontDatabase>(targetFontAddress);
                        fontDB = await handle.Task;
                        loadedHandles[targetFontAddress] = handle;
                        Debug.Log($"[AddressablesLanguageLoader] Addressables から FontDatabase をロード: '{targetFontAddress}'");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AddressablesLanguageLoader] FontDatabase ロード失敗 ({targetFontAddress}): {ex.Message}");
                    }
                }
            }

            // 直参照フォールバック
            if (fontDB == null && config.fontDatabase != null)
            {
                fontDB = config.fontDatabase;
            }

            // --- 3. 不要になった旧アセットのみを選択的にアンロード ---
            CleanupUnusedAssets(targetTextAddress, targetFontAddress);

            activeTextAddress = targetTextAddress;
            activeFontAddress = targetFontAddress;

            return new LoadedLanguageAssets
            {
                LanguageCode = config.languageCode,
                TextTable = textTable,
                FontDatabase = fontDB
            };
        }

        /// <summary>
        /// 新しい言語で使われなくなったアドレスのアセットハンドルだけを安全に解放します。
        /// 同じフォント（アドレス）を使い回している場合は解放されません。
        /// </summary>
        private void CleanupUnusedAssets(string newTextAddress, string newFontAddress)
        {
            var addressesToRemove = new List<string>();

            foreach (var kvp in loadedHandles)
            {
                string addr = kvp.Key;

                // 新しいテキストアドレスでも、新しいフォントアドレスでもない場合は「使用されなくなった」と判断
                bool isStillUsed = (!string.IsNullOrEmpty(newTextAddress) && addr.Equals(newTextAddress, StringComparison.OrdinalIgnoreCase)) ||
                                   (!string.IsNullOrEmpty(newFontAddress) && addr.Equals(newFontAddress, StringComparison.OrdinalIgnoreCase));

                if (!isStillUsed)
                {
                    if (kvp.Value.IsValid())
                    {
                        Addressables.Release(kvp.Value);
                        Debug.Log($"[AddressablesLanguageLoader] 使用されなくなったアセットをアンロード（メモリ解放）しました: '{addr}'");
                    }
                    addressesToRemove.Add(addr);
                }
            }

            foreach (var addr in addressesToRemove)
            {
                loadedHandles.Remove(addr);
            }
        }

        public void ReleaseAssets(LoadedLanguageAssets assets)
        {
            // 全体破棄時（ゲーム終了やマネージャー破棄時）
            foreach (var kvp in loadedHandles)
            {
                if (kvp.Value.IsValid())
                {
                    Addressables.Release(kvp.Value);
                }
            }
            loadedHandles.Clear();
            activeTextAddress = null;
            activeFontAddress = null;
            Debug.Log("[AddressablesLanguageLoader] すべての言語アセットハンドルを解放しました。");
        }
    }
}
