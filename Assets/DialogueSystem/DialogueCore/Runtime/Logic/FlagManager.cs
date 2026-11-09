using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Logic
{
    [Serializable]
    public class FlagData
    {
        public string key;
        public int value;

        public FlagData() { }

        public FlagData(string key, int value)
        {
            this.key = key;
            this.value = value;
        }

        public FlagData(string key, bool value)
        {
            this.key = key;
            this.value = value ? 1 : 0;
        }
    }

    [Serializable]
    public class FlagSaveData
    {
        public List<FlagData> savedFlags = new List<FlagData>();
    }

    /// <summary>
    /// 会話・イベント分岐用の複合条件データ
    /// </summary>
    [Serializable]
    public struct FlagCondition
    {
        public string key;
        public ConditionOperator op;
        public int value;

        public FlagCondition(string key, ConditionOperator op, int value)
        {
            this.key = key;
            this.op = op;
            this.value = value;
        }

        public FlagCondition(string key, bool targetBool)
        {
            this.key = key;
            this.op = ConditionOperator.Equal;
            this.value = targetBool ? 1 : 0;
        }

        public bool IsSatisfied(FlagManager manager)
        {
            if (manager == null) return true;
            return manager.EvaluateCondition(key, op, value);
        }
    }

    public class FlagManager : MonoBehaviour
    {
        private static FlagManager instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        public static FlagManager Instance
        {
            get
            {
                if (instance == null || !instance)
                {
                    instance = FindFirstObjectByType<FlagManager>();
                    if ((instance == null || !instance) && Application.isPlaying)
                    {
                        var go = new GameObject("[FlagManager]");
                        instance = go.AddComponent<FlagManager>();
                        if (go.transform.parent == null) DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        [Header("Initial Flags (ゲーム開始時の初期設定)")]
        [SerializeField] public List<FlagData> initialFlags = new List<FlagData>();

        private readonly Dictionary<string, int> flags = new Dictionary<string, int>();
        public Dictionary<string, int> RuntimeFlags => flags;

        // キー単位のリスナー
        private readonly Dictionary<string, Action<int>> keyListeners = new Dictionary<string, Action<int>>();

        /// <summary>
        /// いずれかのフラグ値が変更されたときに発火するイベント (key, newValue)
        /// </summary>
        public event Action<string, int> OnFlagChanged;

        // セーブファイルの保存先パス（デフォルト）
        public string SavePath => Application.persistentDataPath + "/flags_save.json";

        private void Awake()
        {
            if (instance == null || !instance)
            {
                instance = this;
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
                ResetToInitialFlags();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        // 初期状態にリセットする処理
        public void ResetToInitialFlags()
        {
            flags.Clear();
            if (initialFlags != null)
            {
                foreach (var f in initialFlags)
                {
                    if (string.IsNullOrEmpty(f.key)) continue;
                    flags[f.key] = f.value;
                }
            }
        }

        // ==========================================
        // 🚩 フラグ操作 API (基本 / 数値 / bool / カウント)
        // ==========================================

        public void SetFlag(string key, int value)
        {
            if (string.IsNullOrEmpty(key)) return;

            bool exists = flags.TryGetValue(key, out int prevValue);
            if (!exists || prevValue != value)
            {
                flags[key] = value;
                NotifyChanged(key, value);
            }
        }

        public int GetFlag(string key) => flags.TryGetValue(key, out int val) ? val : 0;

        public int GetFlag(string key, int defaultValue) => flags.TryGetValue(key, out int val) ? val : defaultValue;

        public bool HasFlag(string key) => !string.IsNullOrEmpty(key) && flags.ContainsKey(key);

        public void SetBool(string key, bool value) => SetFlag(key, value ? 1 : 0);

        public bool GetBool(string key, bool defaultValue = false)
        {
            return GetFlag(key, defaultValue ? 1 : 0) != 0;
        }

        /// <summary>
        /// 0 と 1 を反転させ、反転後の真偽値を返します。
        /// </summary>
        public bool Toggle(string key)
        {
            bool next = !GetBool(key);
            SetBool(key, next);
            return next;
        }

        /// <summary>
        /// フラグの数値を加減算し、結果の数値を返します。
        /// </summary>
        public int AddFlag(string key, int delta = 1)
        {
            int next = GetFlag(key) + delta;
            SetFlag(key, next);
            return next;
        }

        /// <summary>
        /// フラグを削除します。
        /// </summary>
        public bool RemoveFlag(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (flags.Remove(key))
            {
                NotifyChanged(key, 0);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 指定したプレフィックスで始まるフラグを一括削除します（例: 一時フラグ "tmp_" や章限定フラグ "ch1_"）
        /// </summary>
        public int RemoveFlagsWithPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return 0;

            var toRemove = new List<string>();
            foreach (var k in flags.Keys)
            {
                if (k.StartsWith(prefix)) toRemove.Add(k);
            }

            foreach (var k in toRemove)
            {
                flags.Remove(k);
                NotifyChanged(k, 0);
            }
            return toRemove.Count;
        }

        public void ClearAllFlags()
        {
            var keys = new List<string>(flags.Keys);
            flags.Clear();
            foreach (var k in keys)
            {
                NotifyChanged(k, 0);
            }
        }

        // ==========================================
        // 🔔 イベント監視 (Reactive API)
        // ==========================================

        /// <summary>
        /// 指定したキーのフラグ値変更時に呼ばれるコールバックを登録します。
        /// </summary>
        public void RegisterListener(string key, Action<int> callback)
        {
            if (string.IsNullOrEmpty(key) || callback == null) return;

            if (keyListeners.TryGetValue(key, out var existing))
            {
                keyListeners[key] = existing + callback;
            }
            else
            {
                keyListeners[key] = callback;
            }
        }

        /// <summary>
        /// コールバックの登録を解除します。
        /// </summary>
        public void UnregisterListener(string key, Action<int> callback)
        {
            if (string.IsNullOrEmpty(key) || callback == null) return;

            if (keyListeners.TryGetValue(key, out var existing))
            {
                var updated = existing - callback;
                if (updated == null)
                {
                    keyListeners.Remove(key);
                }
                else
                {
                    keyListeners[key] = updated;
                }
            }
        }

        private void NotifyChanged(string key, int newValue)
        {
            try
            {
                OnFlagChanged?.Invoke(key, newValue);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            if (keyListeners.TryGetValue(key, out var listener))
            {
                try
                {
                    listener?.Invoke(newValue);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        // ==========================================
        // ⚖️ 条件判定 API (単一 / 複数 / AND / OR)
        // ==========================================

        public bool EvaluateCondition(string key, ConditionOperator op, int targetValue)
        {
            if (string.IsNullOrEmpty(key)) return true;
            int currentValue = GetFlag(key);
            return op switch
            {
                ConditionOperator.Equal => currentValue == targetValue,
                ConditionOperator.NotEqual => currentValue != targetValue,
                ConditionOperator.Greater => currentValue > targetValue,
                ConditionOperator.GreaterOrEqual => currentValue >= targetValue,
                ConditionOperator.Less => currentValue < targetValue,
                ConditionOperator.LessOrEqual => currentValue <= targetValue,
                _ => true,
            };
        }

        /// <summary>
        /// すべての条件を満たしているか評価 (AND)
        /// </summary>
        public bool EvaluateAll(IEnumerable<FlagCondition> conditions)
        {
            if (conditions == null) return true;
            foreach (var cond in conditions)
            {
                if (!cond.IsSatisfied(this)) return false;
            }
            return true;
        }

        /// <summary>
        /// いずれかの条件を満たしているか評価 (OR)
        /// </summary>
        public bool EvaluateAny(IEnumerable<FlagCondition> conditions)
        {
            if (conditions == null) return true;
            bool hasAny = false;
            foreach (var cond in conditions)
            {
                hasAny = true;
                if (cond.IsSatisfied(this)) return true;
            }
            return !hasAny; // 条件が空なら true
        }

        // ==========================================
        // 💾 セーブ＆ロード機能
        // ==========================================

        /// <summary>
        /// ゲーム全体のセーブデータに含めるための DTO をエクスポートします（"tmp_" 一時フラグは除外）。
        /// </summary>
        public FlagSaveData ExportSaveData()
        {
            var data = new FlagSaveData();
            foreach (var kvp in flags)
            {
                if (kvp.Key.StartsWith("tmp_")) continue;
                data.savedFlags.Add(new FlagData { key = kvp.Key, value = kvp.Value });
            }
            return data;
        }

        /// <summary>
        /// DTO からフラグ状態を復元します。
        /// </summary>
        public void ImportSaveData(FlagSaveData data)
        {
            ResetToInitialFlags();
            if (data != null && data.savedFlags != null)
            {
                foreach (var f in data.savedFlags)
                {
                    if (string.IsNullOrEmpty(f.key)) continue;
                    SetFlag(f.key, f.value);
                }
            }
        }

        public string GetSlotPath(int slotIndex) =>
            Path.Combine(Application.persistentDataPath, $"flags_save_slot_{slotIndex}.json");

        public void SaveFlags(string path = null)
        {
            string targetPath = string.IsNullOrEmpty(path) ? SavePath : path;
            var data = ExportSaveData();
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(targetPath, json);
            Debug.Log($"[FlagManager] セーブ完了: {targetPath}\n{json}");
        }

        public void LoadFlags(string path = null)
        {
            string targetPath = string.IsNullOrEmpty(path) ? SavePath : path;
            if (!File.Exists(targetPath))
            {
                Debug.LogWarning($"[FlagManager] セーブデータが見つかりません: {targetPath}");
                return;
            }

            string json = File.ReadAllText(targetPath);
            var data = JsonUtility.FromJson<FlagSaveData>(json);
            ImportSaveData(data);
            Debug.Log($"[FlagManager] ロード完了: {targetPath}");
        }
    }
}
