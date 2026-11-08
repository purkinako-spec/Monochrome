using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Runtime.Dialogue.Core
{
    /// <summary>
    /// 各コンテナの現在のランタイム状態（エディタツールやUI表示用）
    /// </summary>
    [Serializable]
    public class ContainerRuntimeStatus
    {
        public string address;
        public bool isLoaded;
        public DialogueContainer container;
        public int nodeCount;
        public List<string> labels = new List<string>();
    }

    /// <summary>
    /// ゲーム全体の DialogueContainer のロード・アンロード・キャッシュを一元管理するマネージャー。
    /// （個別アドレス指定に加え、複数ラベルによるグループ一括ロード・アンロードに対応）
    /// </summary>
    [AddComponentMenu("Dialogue/Core/Dialogue Container Manager")]
    [DisallowMultipleComponent]
    public class DialogueContainerManager : MonoBehaviour
    {
        private static DialogueContainerManager instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        public static DialogueContainerManager Instance
        {
            get
            {
                if (instance == null || !instance)
                {
                    instance = FindFirstObjectByType<DialogueContainerManager>();
                    if ((instance == null || !instance) && Application.isPlaying)
                    {
                        var go = new GameObject("[DialogueContainerManager]");
                        instance = go.AddComponent<DialogueContainerManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        // ロード済みコンテナのキャッシュ (Address -> Container)
        private readonly Dictionary<string, DialogueContainer> loadedContainers = new Dictionary<string, DialogueContainer>(StringComparer.OrdinalIgnoreCase);

        // Addressables ハンドル (Address -> Handle)
        private readonly Dictionary<string, AsyncOperationHandle<DialogueContainer>> loadedHandles = new Dictionary<string, AsyncOperationHandle<DialogueContainer>>(StringComparer.OrdinalIgnoreCase);

        // ラベルによるグループハンドル (Label -> Handle)
        private readonly Dictionary<string, AsyncOperationHandle<IList<DialogueContainer>>> labelHandles = new Dictionary<string, AsyncOperationHandle<IList<DialogueContainer>>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// コンテナのロード状態が変化した時に通知されるイベント (address, isLoaded)
        /// </summary>
        public static event Action<string, bool> OnContainerStatusChanged;

        private void Awake()
        {
            if (instance == null || !instance)
            {
                instance = this;
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// コンテナがメモリ上にロードされているか確認します
        /// </summary>
        public bool IsLoaded(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;
            return loadedContainers.ContainsKey(address);
        }

        /// <summary>
        /// ロード済みのコンテナを取得します（未ロードなら null）
        /// </summary>
        public DialogueContainer GetLoaded(string address)
        {
            if (string.IsNullOrEmpty(address)) return null;
            loadedContainers.TryGetValue(address, out var c);
            return c;
        }

        /// <summary>
        /// Addressables アドレスを指定してコンテナを非同期ロードします。
        /// 既にロード済みの場合はキャッシュを即座に返します。
        /// </summary>
        public async Task<DialogueContainer> LoadContainerAsync(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                Debug.LogWarning("[DialogueContainerManager] アドレスが空です。");
                return null;
            }

            // キャッシュ確認
            if (loadedContainers.TryGetValue(address, out var cached) && cached != null)
            {
                return cached;
            }

            try
            {
                var handle = Addressables.LoadAssetAsync<DialogueContainer>(address);
                var container = await handle.Task;

                if (container != null)
                {
                    loadedContainers[address] = container;
                    loadedHandles[address] = handle;
                    Debug.Log($"<color=lime>[DialogueContainerManager]</color> コンテナをロードしました: <b>{address}</b> (ノード数: {container.allNodes?.Count ?? 0})");
                    OnContainerStatusChanged?.Invoke(address, true);
                    return container;
                }
                else
                {
                    Debug.LogError($"[DialogueContainerManager] コンテナのロード結果が null です: {address}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DialogueContainerManager] コンテナロード失敗 ({address}): {ex.Message}");
                return null;
            }
        }

        // --- 🏷️ ラベル（グループ）による一括ロード・アンロード機能 ---

        /// <summary>
        /// 指定されたラベル（例: "TownA", "Chapter1"）が付いたすべてのコンテナを一括非同期ロードします。
        /// </summary>
        public async Task<IList<DialogueContainer>> LoadByLabelAsync(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;

            try
            {
                // すでにラベルロード済みならそのリストを返す
                if (labelHandles.TryGetValue(label, out var existingHandle) && existingHandle.IsValid())
                {
                    return existingHandle.Result;
                }

                var handle = Addressables.LoadAssetsAsync<DialogueContainer>(label, container =>
                {
                    if (container != null)
                    {
                        // 各コンテナ名でもキャッシュ登録
                        string name = container.name;
                        loadedContainers[name] = container;
                        OnContainerStatusChanged?.Invoke(name, true);
                    }
                });

                var list = await handle.Task;
                labelHandles[label] = handle;
                Debug.Log($"<color=lime>[DialogueContainerManager]</color> ラベル <b>[{label}]</b> のコンテナを一括ロードしました (件数: {list?.Count ?? 0})");
                return list;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DialogueContainerManager] ラベル [{label}] の一括ロード失敗: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 指定されたラベル（例: "TownA"）の一括ロードハンドルを解放（アンロード）します。
        /// </summary>
        public void UnloadByLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return;

            if (labelHandles.TryGetValue(label, out var handle))
            {
                if (handle.IsValid())
                {
                    if (handle.Result != null)
                    {
                        foreach (var c in handle.Result)
                        {
                            if (c != null)
                            {
                                loadedContainers.Remove(c.name);
                                OnContainerStatusChanged?.Invoke(c.name, false);
                            }
                        }
                    }
                    Addressables.Release(handle);
                }
                labelHandles.Remove(label);
                Debug.Log($"<color=orange>[DialogueContainerManager]</color> ラベル <b>[{label}]</b> のコンテナを一括アンロード（メモリ解放）しました。");
            }
        }

        /// <summary>
        /// 直参照のアセットをキャッシュに直接登録します（手動登録・フォールバック用）
        /// </summary>
        public void RegisterDirectContainer(string address, DialogueContainer container)
        {
            if (string.IsNullOrEmpty(address) || container == null) return;
            loadedContainers[address] = container;
            OnContainerStatusChanged?.Invoke(address, true);
        }

        /// <summary>
        /// 指定された個別コンテナをメモリからアンロード（解放）します
        /// </summary>
        public void UnloadContainer(string address)
        {
            if (string.IsNullOrEmpty(address)) return;

            bool wasLoaded = false;

            if (loadedHandles.TryGetValue(address, out var handle))
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
                loadedHandles.Remove(address);
                wasLoaded = true;
            }

            if (loadedContainers.Remove(address))
            {
                wasLoaded = true;
            }

            if (wasLoaded)
            {
                Debug.Log($"<color=orange>[DialogueContainerManager]</color> コンテナをアンロード（メモリ解放）しました: <b>{address}</b>");
                OnContainerStatusChanged?.Invoke(address, false);
            }
        }

        /// <summary>
        /// ロードされているすべてのコンテナ・ラベルを一括アンロードします
        /// </summary>
        public void UnloadAll()
        {
            foreach (var kvp in labelHandles)
            {
                if (kvp.Value.IsValid()) Addressables.Release(kvp.Value);
            }
            labelHandles.Clear();

            var keys = new List<string>(loadedContainers.Keys);
            foreach (var k in keys)
            {
                UnloadContainer(k);
            }
            Debug.Log("[DialogueContainerManager] すべてのコンテナとラベルをアンロードしました。");
        }

        /// <summary>
        /// 現在ロード中のすべてのコンテナ状態を取得します（ツール・デバッグ用）
        /// </summary>
        public List<ContainerRuntimeStatus> GetLoadedStatusList()
        {
            var list = new List<ContainerRuntimeStatus>();
            foreach (var kvp in loadedContainers)
            {
                list.Add(new ContainerRuntimeStatus
                {
                    address = kvp.Key,
                    isLoaded = true,
                    container = kvp.Value,
                    nodeCount = kvp.Value != null && kvp.Value.allNodes != null ? kvp.Value.allNodes.Count : 0
                });
            }
            return list;
        }
    }
}
