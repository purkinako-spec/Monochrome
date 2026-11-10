using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Monochrome.Core.Character;
using Monochrome.Core.Transition;
using Runtime.Dialogue.Logic;

namespace Monochrome.Core.Save
{
    [Serializable]
    public class GameSaveData
    {
        public int saveVersion = 1;
        public string saveTimestamp = "";
        public string sceneName = "";
        public string spawnPointId = "Default";
        public Vector2 playerPosition;
        public Vector2 playerFacing = Vector2.down;

        // フラグデータ（FlagManager の DTO）
        public FlagSaveData flagData = new FlagSaveData();
    }

    /// <summary>
    /// ゲーム全体の進行状況（シーン、プレイヤー位置・向き、FlagManager のフラグ）を
    /// 一括でセーブ・ロードする統合マネージャー。
    /// 原子的書き込み（.tmp -> Move）とバックアップ（.bak）による安全な永続化を提供します。
    /// </summary>
    [AddComponentMenu("Monochrome/Save/Game Save Manager")]
    public class GameSaveManager : MonoBehaviour
    {
        private static GameSaveManager instance;

        public static GameSaveManager Instance
        {
            get
            {
                if (instance == null || !instance)
                {
                    instance = FindFirstObjectByType<GameSaveManager>();
                    if ((instance == null || !instance) && Application.isPlaying)
                    {
                        var go = new GameObject("[GameSaveManager]");
                        instance = go.AddComponent<GameSaveManager>();
                        if (go.transform.parent == null) DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance == null || !instance)
            {
                instance = this;
                if (transform.parent == null) DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private string GetSavePath(int slotIndex) =>
            Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");

        /// <summary>
        /// 現在のゲーム状態をスロットに保存します
        /// </summary>
        public bool SaveGame(int slotIndex = 0)
        {
            try
            {
                var data = new GameSaveData
                {
                    saveTimestamp = DateTime.UtcNow.ToString("o"),
                    sceneName     = SceneManager.GetActiveScene().name
                };

                // プレイヤー位置と向き
                if (PlayerController2D.LocalPlayer != null)
                {
                    data.playerPosition = PlayerController2D.LocalPlayer.transform.position;
                    data.playerFacing   = PlayerController2D.LocalPlayer.Motor.FacingDirection;
                }

                // フラグデータ
                if (FlagManager.Instance != null)
                {
                    data.flagData = FlagManager.Instance.ExportSaveData();
                }

                string json = JsonUtility.ToJson(data, true);
                string path = GetSavePath(slotIndex);
                string tmpPath = path + ".tmp";

                File.WriteAllText(tmpPath, json, Encoding.UTF8);

                // 前回ファイルがあればバックアップ
                if (File.Exists(path))
                {
                    File.Copy(path, path + ".bak", overwrite: true);
                }
                File.Move(tmpPath, path);

                Debug.Log($"[GameSaveManager] スロット {slotIndex} にセーブ完了: {path}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameSaveManager] セーブ失敗: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// スロットからゲーム状態をロードし、シーンとプレイヤーを復元します
        /// </summary>
        public async Task<bool> LoadGame(int slotIndex = 0)
        {
            await Task.Yield();
            string path = GetSavePath(slotIndex);
            if (!File.Exists(path))
            {
                // バックアップを試行
                path += ".bak";
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[GameSaveManager] セーブデータが見つかりません: {path}");
                    return false;
                }
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var data = JsonUtility.FromJson<GameSaveData>(json);

                // 1. フラグの復元
                if (FlagManager.Instance != null && data.flagData != null)
                {
                    FlagManager.Instance.ImportSaveData(data.flagData);
                }

                // 2. エリア移動マネージャーを使って指定シーンと座標へ復元
                var request = new TransitionRequest
                {
                    destinationScene = data.sceneName,
                    spawnPointId     = data.spawnPointId,
                    style            = TransitionStyle.LoadingScreen,
                    onComplete = () =>
                    {
                        if (PlayerController2D.LocalPlayer != null)
                        {
                            PlayerController2D.LocalPlayer.Motor.WarpTo(data.playerPosition, data.playerFacing);
                        }
                    }
                };

                AreaTransitionManager.Instance.Transition(request);
                Debug.Log($"[GameSaveManager] スロット {slotIndex} からロード成功");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameSaveManager] ロード失敗: {ex.Message}");
                return false;
            }
        }
    }
}
