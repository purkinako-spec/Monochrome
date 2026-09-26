using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using MiniGame.Fish.Test1;

namespace MiniGame.Fish.Test2
{
    /// <summary>
    /// ミニゲーム全体の進行・スポーン管理クラス
    /// 3つのエリアにそれぞれ魚をスポーンさせ、ルアーによる釣り上げを管理します。
    /// 全て釣り上げた際にはクリア演出を表示し、自動的にシーンをアンロードして終了します。
    /// </summary>
    public class FishingMinigameManager : MonoBehaviour
    {
        [Header("魚プレハブ & 出現候補 (ScriptableObject)")]
        [Tooltip("エリアごとに生成する魚のPrefab (FishMovementがアタッチされているもの)")]
        [SerializeField] private GameObject fishPrefab;
        [Tooltip("既存のFishStatusリスト。確率(rate)に基づいて抽選されます")]
        [SerializeField] private List<FishStatus> fishStatusList = new List<FishStatus>();

        [Header("スポーンエリア (3か所)")]
        [Tooltip("魚をそれぞれ1匹ずつ泳がせる3つのエリア")]
        [SerializeField] private Collider2D[] spawnAreas = new Collider2D[3];

        [Header("ルアー")]
        [SerializeField] private LureMovement lure;

        [Header("シーン配置済みの魚 (Prefab生成ではなく配置済みの魚を使う場合)")]
        [Tooltip("Prefabではなく手動配置した魚オブジェクトを使う場合はここに登録（3匹）")]
        [SerializeField] private List<FishMovement> preplacedFish = new List<FishMovement>();

        [Header("クリア演出 & シーン終了設定")]
        [Tooltip("アンロード対象のミニゲームシーン名")]
        [SerializeField] private string sceneNameToUnload = "FishingMinigameScene";
        [Tooltip("全匹釣り上げ後、シーンを閉じるまでの余韻・演出待機秒数")]
        [SerializeField] private float clearWaitDuration = 2.0f;
        [Tooltip("クリア時に表示するUIパネルやテキストオブジェクト（任意）")]
        [SerializeField] private GameObject gameClearUI;

        [Header("演出・UI用イベント")]
        [SerializeField] private UnityEvent<FishStatus> onFishCaughtEvent;
        [SerializeField] private UnityEvent onAllFishCaughtEvent;

        // C# イベント
        public event Action<FishMovement, FishStatus> OnFishCaught;
        public event Action OnAllFishCaught;

        // 進行状態
        private readonly List<FishMovement> activeFishList = new List<FishMovement>();
        private int caughtCount = 0;
        private bool isGameCleared = false;

        public IReadOnlyList<FishMovement> ActiveFishList => activeFishList;
        public int CaughtCount => caughtCount;
        public int TotalFishCount => activeFishList.Count;

        private void Start()
        {
            if (gameClearUI != null) gameClearUI.SetActive(false);
            StartMinigame();
        }

        /// <summary>
        /// ミニゲーム開始・3エリアへの魚スポーン処理
        /// </summary>
        public void StartMinigame()
        {
            ClearExistingFish();
            caughtCount = 0;
            isGameCleared = false;

            if (lure == null)
            {
                Debug.LogWarning("[FishingMinigameManager] ルアー(LureMovement)が設定されていません。");
            }

            // 1. 手動配置済みの魚がある場合
            if (preplacedFish != null && preplacedFish.Count > 0)
            {
                for (int i = 0; i < preplacedFish.Count; i++)
                {
                    FishMovement fish = preplacedFish[i];
                    if (fish == null) continue;

                    Collider2D area = (i < spawnAreas.Length) ? spawnAreas[i] : null;
                    FishStatus status = GetRandomFishData();

                    fish.Initialize(status, area, lure != null ? lure.transform : null);
                    RegisterFish(fish);
                }
            }
            // 2. Prefabから3つのエリアにスポーンする場合
            else if (fishPrefab != null)
            {
                for (int i = 0; i < spawnAreas.Length; i++)
                {
                    Collider2D area = spawnAreas[i];
                    if (area == null)
                    {
                        Debug.LogWarning($"[FishingMinigameManager] スポーンエリア {i} が未割り当てです。");
                        continue;
                    }

                    FishStatus status = GetRandomFishData();
                    GameObject fishObj = Instantiate(fishPrefab, transform);
                    FishMovement fishMovement = fishObj.GetComponent<FishMovement>();

                    if (fishMovement != null)
                    {
                        fishMovement.Initialize(status, area, lure != null ? lure.transform : null);
                        RegisterFish(fishMovement);
                    }
                    else
                    {
                        Debug.LogError("[FishingMinigameManager] fishPrefab に FishMovement コンポーネントがありません。");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[FishingMinigameManager] fishPrefab または preplacedFish を設定してください。");
            }
        }

        private void RegisterFish(FishMovement fish)
        {
            activeFishList.Add(fish);
            fish.OnCaught += HandleFishCaught;
        }

        /// <summary>
        /// 既存の FishStatus データ構造の重み付け抽選（Test1のロジックを忠実に再現・維持）
        /// </summary>
        public FishStatus GetRandomFishData()
        {
            if (fishStatusList == null || fishStatusList.Count == 0)
            {
                Debug.LogWarning("[FishingMinigameManager] FishStatusList が空です。");
                return null;
            }

            float totalRate = 0f;
            foreach (FishStatus status in fishStatusList)
            {
                if (status != null) totalRate += status.rate;
            }

            float targetRate = UnityEngine.Random.Range(0f, totalRate);
            float tmpTotalRate = 0f;

            foreach (FishStatus status in fishStatusList)
            {
                if (status == null) continue;
                tmpTotalRate += status.rate;

                if (tmpTotalRate >= targetRate)
                {
                    return status;
                }
            }

            return fishStatusList[0];
        }

        /// <summary>
        /// 魚が釣り上げられた際のコールバック
        /// </summary>
        private void HandleFishCaught(FishMovement fish)
        {
            if (isGameCleared) return;

            caughtCount++;

            string fishName = fish.Status != null ? fish.Status.fishName : "Unknown Fish";
            Debug.Log($"[FishingMinigameManager] 釣り上げ成功: {fishName} ({caughtCount}/{activeFishList.Count})");

            // 演出・通知用イベントを発火
            OnFishCaught?.Invoke(fish, fish.Status);
            onFishCaughtEvent?.Invoke(fish.Status);

            // 全ての魚を釣り上げたかチェック
            if (caughtCount >= activeFishList.Count)
            {
                isGameCleared = true;
                StartCoroutine(AllFishCaughtSequenceCoroutine());
            }
        }

        /// <summary>
        /// 全匹釣り上げ時の演出およびシーン終了コルーチン
        /// </summary>
        private IEnumerator AllFishCaughtSequenceCoroutine()
        {
            Debug.Log("[FishingMinigameManager] 全エリアの魚を釣り上げました！クリア演出を開始します。");

            // 1. ルアーの操作を停止
            if (lure != null)
            {
                lure.SetCanInput(false);
            }

            // 2. クリアUIがあれば表示
            if (gameClearUI != null)
            {
                gameClearUI.SetActive(true);
            }

            // 3. クリア通知を発火（SEや追加演出のトリガーに利用可能）
            OnAllFishCaught?.Invoke();
            onAllFishCaughtEvent?.Invoke();

            // 4. 演出の余韻を待つ
            yield return new WaitForSeconds(clearWaitDuration);

            // 5. シーンを終了
            CloseMinigameScene();
        }

        /// <summary>
        /// ミニゲームシーンのクローズ処理
        /// </summary>
        private void CloseMinigameScene()
        {
            Debug.Log($"[FishingMinigameManager] ミニゲーム終了。シーン '{sceneNameToUnload}' を閉じます。");

            // 加算ロードされたシーンが存在していればアンロード
            if (SceneManager.sceneCount > 1)
            {
                SceneManager.UnloadSceneAsync(sceneNameToUnload);
            }
            else
            {
                Debug.LogWarning($"[FishingMinigameManager] 現在単一シーンとして開かれているため、UnloadSceneAsyncは実行されませんでした。(シーン名: {SceneManager.GetActiveScene().name})");
            }
        }

        private void ClearExistingFish()
        {
            foreach (FishMovement fish in activeFishList)
            {
                if (fish != null && fish.gameObject != null)
                {
                    fish.OnCaught -= HandleFishCaught;
                    if (!preplacedFish.Contains(fish))
                    {
                        Destroy(fish.gameObject);
                    }
                }
            }
            activeFishList.Clear();
        }

        private void OnDestroy()
        {
            foreach (FishMovement fish in activeFishList)
            {
                if (fish != null)
                {
                    fish.OnCaught -= HandleFishCaught;
                }
            }
        }
    }
}
