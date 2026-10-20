using System.Collections.Generic;
using UnityEngine;
using System.IO; // 👈 追加: ファイル保存に必要
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Logic
{
    [System.Serializable]
    public class FlagData
    {
        public string key;
        public int value;
    }

    // 💡 追加: セーブデータをJSON化するための入れ物（ラッパークラス）
    [System.Serializable]
    public class FlagSaveData
    {
        public List<FlagData> savedFlags = new List<FlagData>();
    }

    public class FlagManager : MonoBehaviour
    {
        public static FlagManager Instance { get; private set; }

        [Header("Initial Flags (ゲーム開始時の初期設定)")]
        [SerializeField] public List<FlagData> initialFlags = new List<FlagData>();

        private Dictionary<string, int> flags = new Dictionary<string, int>();
        public Dictionary<string, int> RuntimeFlags => flags;

        // セーブファイルの保存先パス
        private string SavePath => Application.persistentDataPath + "/flags_save.json";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                ResetToInitialFlags();
            }
            else Destroy(gameObject);
        }

        // 初期状態にリセットする処理
        public void ResetToInitialFlags()
        {
            flags.Clear();
            foreach (var f in initialFlags)
            {
                flags[f.key] = f.value;
            }
        }

        public void SetFlag(string key, int value) => flags[key] = value;
        public int GetFlag(string key) => flags.TryGetValue(key, out int val) ? val : 0;

        // ==========================================
        // 💾 セーブ＆ロード機能
        // ==========================================
        public void SaveFlags()
        {
            FlagSaveData data = new FlagSaveData();

            foreach (var kvp in flags)
            {
                // 💡 フィルタリング: "tmp_" で始まるキーは保存せずスキップ（破棄）
                if (kvp.Key.StartsWith("tmp_")) continue;

                data.savedFlags.Add(new FlagData { key = kvp.Key, value = kvp.Value });
            }

            // JSONテキストに変換してファイル書き出し
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

            // 一旦初期フラグ状態に戻す（セーブデータにない初期フラグを補完するため）
            ResetToInitialFlags();

            // セーブデータの内容で上書き
            foreach (var f in data.savedFlags)
            {
                flags[f.key] = f.value;
            }
            Debug.Log("[FlagManager] ロード完了！");
        }

        // 条件判定（既存のまま）
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