using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniGame.Fish.Test2
{
    /// <summary>
    /// シーン遷移を管理するシングルトンローダー
    /// FishingSpotProcessor 等から参照されることを想定
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 指定したシーンを Additive で読み込む
        /// </summary>
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneLoader] シーン名が指定されていません。");
                return;
            }

            // 既に読み込まれているかチェック
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                Debug.Log($"[SceneLoader] シーン '{sceneName}' は既に読み込まれています。");
                return;
            }

            Debug.Log($"[SceneLoader] シーン読み込み開始: {sceneName} (Additive)");
            SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
        }

        /// <summary>
        /// 指定したシーンをアンロードする
        /// </summary>
        public void UnloadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            SceneManager.UnloadSceneAsync(sceneName);
        }
    }
}
