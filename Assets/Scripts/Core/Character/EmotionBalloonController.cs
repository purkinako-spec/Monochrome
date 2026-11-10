using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Monochrome.Core.Character
{
    public enum BalloonType
    {
        Exclamation, // ！ (発見・驚き)
        Question,    // ？ (疑問)
        Idea,        // 電球 / 閃き
        Sweat,       // 汗 / 焦り・困惑
        Heart,       // ハート / 好意
        Dots         // … / 沈黙
    }

    /// <summary>
    /// キャラクターの頭上に感情バルーン（！や？のアイコン）をポップアップさせるコントローラー。
    /// スケールアニメーションと自動破棄を行い、イベント演出を盛り上げます。
    /// </summary>
    [AddComponentMenu("Monochrome/Character/Emotion Balloon Controller")]
    public class EmotionBalloonController : MonoBehaviour
    {
        [Header("Anchor & Offset")]
        [Tooltip("バルーンの出現位置。未指定の場合は自身の頭上自動計算")]
        [SerializeField] private Transform balloonAnchor;
        [SerializeField] private Vector3 offset = new Vector3(0, 1.2f, 0);

        [Header("Animation Settings")]
        [SerializeField] private float displayDuration = 1.2f;
        [SerializeField] private float popAnimationDuration = 0.2f;

        [Header("Optional Sprite Overrides")]
        [SerializeField] private Sprite exclamationSprite;
        [SerializeField] private Sprite questionSprite;
        [SerializeField] private Sprite ideaSprite;
        [SerializeField] private Sprite sweatSprite;
        [SerializeField] private Sprite heartSprite;
        [SerializeField] private Sprite dotsSprite;

        private GameObject currentBalloonInstance;
        private Coroutine activePopRoutine;

        /// <summary>
        /// 頭上に感情バルーンを表示します
        /// </summary>
        public void ShowBalloon(BalloonType type, float? duration = null)
        {
            if (activePopRoutine != null)
            {
                StopCoroutine(activePopRoutine);
            }
            if (currentBalloonInstance != null)
            {
                Destroy(currentBalloonInstance);
            }

            activePopRoutine = StartCoroutine(SpawnAndAnimateBalloon(type, duration ?? displayDuration));
        }

        private IEnumerator SpawnAndAnimateBalloon(BalloonType type, float duration)
        {
            Vector3 spawnPos = balloonAnchor != null ? balloonAnchor.position : transform.position + offset;

            // バルーン用 GameObject の動的生成
            currentBalloonInstance = new GameObject($"[Balloon_{type}]");
            currentBalloonInstance.transform.SetParent(transform);
            currentBalloonInstance.transform.position = spawnPos;

            var sr = currentBalloonInstance.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 100; // 最前面に表示
            sr.sprite = GetSpriteForType(type);

            // ポップアップ拡縮アニメーション (0 -> 1.2 -> 1.0)
            float elapsed = 0f;
            currentBalloonInstance.transform.localScale = Vector3.zero;

            while (elapsed < popAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popAnimationDuration);
                // オーバーシュート（少し大きくなって戻る）
                float scale = Mathf.Sin(t * Mathf.PI * 0.5f) * 1.15f;
                currentBalloonInstance.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            currentBalloonInstance.transform.localScale = Vector3.one;

            // 表示待機
            yield return new WaitForSeconds(duration);

            // シュリンクして消去
            elapsed = 0f;
            while (elapsed < popAnimationDuration * 0.5f)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / (popAnimationDuration * 0.5f));
                currentBalloonInstance.transform.localScale = Vector3.one * t;
                yield return null;
            }

            Destroy(currentBalloonInstance);
            currentBalloonInstance = null;
            activePopRoutine = null;
        }

        private Sprite GetSpriteForType(BalloonType type) => type switch
        {
            BalloonType.Exclamation => exclamationSprite,
            BalloonType.Question    => questionSprite,
            BalloonType.Idea        => ideaSprite,
            BalloonType.Sweat       => sweatSprite,
            BalloonType.Heart       => heartSprite,
            BalloonType.Dots        => dotsSprite,
            _                       => exclamationSprite
        };

        private void OnDisable()
        {
            if (currentBalloonInstance != null)
            {
                Destroy(currentBalloonInstance);
                currentBalloonInstance = null;
            }
        }
    }
}
