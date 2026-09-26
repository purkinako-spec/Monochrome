using UnityEngine;
using UnityEngine.UI;

namespace MiniGame.Fish.Test2
{
    /// <summary>
    /// （任意コンポーネント）魚の頭上などに釣り上げゲージを表示したい場合のUI連携スクリプト
    /// </summary>
    public class FishGaugeUI : MonoBehaviour
    {
        [Tooltip("ゲージを連動させる対象の魚")]
        [SerializeField] private FishMovement targetFish;
        [Tooltip("進捗を表示するUI Slider")]
        [SerializeField] private Slider gaugeSlider;
        [Tooltip("ゲージ全体の親オブジェクト（ルアー接近時のみ表示したい場合）")]
        [SerializeField] private GameObject gaugeRoot;
        [Tooltip("ルアーが離れている間は非表示にするかどうか")]
        [SerializeField] private bool hideWhenFar = true;

        private void Start()
        {
            if (targetFish == null)
            {
                targetFish = GetComponentInParent<FishMovement>();
            }

            if (targetFish != null)
            {
                targetFish.OnGaugeChanged += HandleGaugeChanged;
                targetFish.OnLureEnter += HandleLureEnter;
                targetFish.OnLureExit += HandleLureExit;
            }

            if (hideWhenFar && gaugeRoot != null)
            {
                gaugeRoot.SetActive(false);
            }
        }

        private void HandleGaugeChanged(float current, float max)
        {
            if (gaugeSlider != null)
            {
                gaugeSlider.value = max > 0 ? current / max : 0f;
            }
        }

        private void HandleLureEnter()
        {
            if (hideWhenFar && gaugeRoot != null)
            {
                gaugeRoot.SetActive(true);
            }
        }

        private void HandleLureExit()
        {
            if (hideWhenFar && gaugeRoot != null && targetFish != null && targetFish.CurrentGauge <= 0f)
            {
                gaugeRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (targetFish != null)
            {
                targetFish.OnGaugeChanged -= HandleGaugeChanged;
                targetFish.OnLureEnter -= HandleLureEnter;
                targetFish.OnLureExit -= HandleLureExit;
            }
        }
    }
}
