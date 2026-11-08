using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Logic
{
    [System.Serializable]
    public class FlagData
    {
        public string key;
        public int value;
    }

    [System.Serializable]
    public class FlagSaveData
    {
        public List<FlagData> savedFlags = new List<FlagData>();
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

        private Dictionary<string, int> flags = new Dictionary<string, int>();
        public Dictionary<string, int> RuntimeFlags => flags;

        // セーブファイルの保存先パス
        private string SavePath => Application.persistentDataPath + "/flags_save.json";

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
                    flags[f.key] = f.value;
                }
            }
        }

        public void SetFlag(string key, int value) => flags[key] = value;
        public int GetFlag(string key) => flags.TryGetValue(key, out int val) ? val : 0;

        // ==========================================
        // 💾 セーブ・ロード機能
        // ==========================================
        public void SaveFlags()
        {
            FlagSaveData data = new FlagSaveData();

            foreach (var kvp in flags)
            {
                // "tmp_" で始まる一時フラグは除外
                if (kvp.Key.StartsWith("tmp_")) continue;

                data.savedFlags.Add(new FlagData { key = kvp.Key, value = kvp.Value });
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[FlagManager] セーブ完了: {SavePath}\n{json}");
        }

        public void LoadFlags()
        {
            if (!File.Exists(SavePath))
            {
                Debug.LogWarning("[FlagManager] セーブデータが見つかりません。");
                return;
            }

            string json = File.ReadAllText(SavePath);
            FlagSaveData data = JsonUtility.FromJson<FlagSaveData>(json);

            ResetToInitialFlags();

            if (data != null && data.savedFlags != null)
            {
                foreach (var f in data.savedFlags)
                {
                    flags[f.key] = f.value;
                }
            }
            Debug.Log("[FlagManager] ロード完了");
        }

        // 条件判定
        public bool EvaluateCondition(string key, ConditionOperator op, int targetValue)
        {
            if (string.IsNullOrEmpty(key)) return true;
            int currentValue = GetFlag(key);
            return op switch
            {
                ConditionOperator.Equal => currentValue == targetValue,
                ConditionOperator.Greater => currentValue > targetValue,
                ConditionOperator.GreaterOrEqual => currentValue >= targetValue,
                ConditionOperator.Less => currentValue < targetValue,
                ConditionOperator.LessOrEqual => currentValue <= targetValue,
                _ => true,
            };
        }
    }
}
