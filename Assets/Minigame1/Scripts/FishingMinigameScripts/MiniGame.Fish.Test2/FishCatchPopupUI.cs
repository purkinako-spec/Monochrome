using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MiniGame.Fish.Test1;

namespace MiniGame.Fish.Test2
{
    /// <summary>
    /// 魚を釣り上げた際に画面上に名前と画像を表示するポップアップUIスクリプト
    /// FishingMinigameManagerのイベントを受信して自動で表示・一定時間後に非表示にします。
    /// </summary>
    public class FishCatchPopupUI : MonoBehaviour
    {
        [Header("マネージャー参照（空欄の場合は自動検索）")]
        [SerializeField] private FishingMinigameManager minigameManager;

        [Header("UI要素の参照")]
        [Tooltip("ポップアップ全体のルートオブジェクト（非表示制御用）")]
        [SerializeField] private GameObject popupRoot;
        [Tooltip("魚の画像を表示するUI Image")]
        [SerializeField] private Image fishImage;
        [Tooltip("魚の名前を表示するTextMeshPro（TMPを使用する場合）")]
        [SerializeField] private TextMeshProUGUI fishNameTMP;
        [Tooltip("魚の名前を表示する標準Text（標準UI Textを使用する場合）")]
        [SerializeField] private Text fishNameText;

        [Header("追加情報（任意）")]
        [Tooltip("魚の紹介文や説明を表示するテキスト（任意）")]
        [SerializeField] private TextMeshProUGUI fishProfileTMP;

        [Header("表示演出設定")]
        [Tooltip("画面に表示し続ける秒数")]
        [SerializeField] private float displayDuration = 2.5f;
        [Tooltip("時間経過で自動的に閉じるかどうか")]
        [SerializeField] private bool autoHide = true;
        [Tooltip("表示時にふわっと拡大する演出を行うか")]
        [SerializeField] private bool animateScale = true;

        private Coroutine hideCoroutine;
        private Vector3 originalScale = Vector3.one;

        private void Awake()
        {
            if (popupRoot == null)
            {
                popupRoot = gameObject;
            }

            originalScale = popupRoot.transform.localScale;
            popupRoot.SetActive(false);
        }

        private void Start()
        {
            // マネージャーの自動検索
            if (minigameManager == null)
            {
                minigameManager = FindFirstObjectByType<FishingMinigameManager>();
            }

            // 釣り上げイベントの購読
            if (minigameManager != null)
            {
                minigameManager.OnFishCaught += HandleFishCaught;
            }
        }

        private void OnDestroy()
        {
            if (minigameManager != null)
            {
                minigameManager.OnFishCaught -= HandleFishCaught;
            }
        }

        /// <summary>
        /// 釣り上げイベント受信時のハンドラ
        /// </summary>
        private void HandleFishCaught(FishMovement fish, FishStatus status)
        {
            ShowFishInfo(status);
        }

        /// <summary>
        /// 指定された魚の情報をUIに反映して表示
        /// </summary>
        public void ShowFishInfo(FishStatus status)
        {
            if (status == null) return;

            // 1. 画像の反映
            if (fishImage != null)
            {
                if (status.fishImage != null)
                {
                    fishImage.sprite = status.fishImage;
                    fishImage.enabled = true;
                    // アスペクト比を維持
                    fishImage.preserveAspect = true;
                }
                else
                {
                    fishImage.enabled = false;
                }
            }

            // 2. 名前の反映
            string nameString = $"{status.fishName} をつりあげた！";
            if (fishNameTMP != null)
            {
                fishNameTMP.text = nameString;
            }
            if (fishNameText != null)
            {
                fishNameText.text = nameString;
            }

            // 3. 説明文の反映（あれば）
            if (fishProfileTMP != null)
            {
                fishProfileTMP.text = status.profile;
            }

            // 4. UIの表示と演出
            popupRoot.SetActive(true);

            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
            }

            hideCoroutine = StartCoroutine(ShowPopupCoroutine());
        }

        /// <summary>
        /// ポップアップの表示・待機・非表示コルーチン
        /// </summary>
        private IEnumerator ShowPopupCoroutine()
        {
            // ポップアニメーション（小さく出現して元の大きさに）
            if (animateScale)
            {
                float animTime = 0.2f;
                float elapsed = 0f;
                popupRoot.transform.localScale = originalScale * 0.5f;

                while (elapsed < animTime)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / animTime;
                    popupRoot.transform.localScale = Vector3.Lerp(originalScale * 0.5f, originalScale, t);
                    yield return null;
                }

                popupRoot.transform.localScale = originalScale;
            }

            // 指定秒数表示
            if (autoHide)
            {
                yield return new WaitForSeconds(displayDuration);
                popupRoot.SetActive(false);
            }
        }

        /// <summary>
        /// 手動でポップアップを閉じる場合
        /// </summary>
        public void Hide()
        {
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
            }
            popupRoot.SetActive(false);
        }
    }
}
