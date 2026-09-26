using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using MiniGame.Fish.Test1;

namespace MiniGame.Fish.Test2
{
    /// <summary>
    /// 各エリア内を泳ぐ魚の制御スクリプト
    /// ルアーが接近するとゲージが上昇し、満タンになると釣り上げ演出が発生します。
    /// 演出やUIを後から接続しやすいように各種イベント（C# Action / UnityEvent）を用意しています。
    /// </summary>
    public class FishMovement : MonoBehaviour
    {
        [Header("魚データ (ScriptableObject)")]
        [SerializeField] private FishStatus fishStatus;

        [Header("移動設定")]
        [SerializeField] private Collider2D moveArea;
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float waitTime = 1f;

        [Header("釣り上げゲージ設定")]
        [Tooltip("ルアーと魚がこの距離以内に入るとゲージが溜まります")]
        [SerializeField] private float catchDistance = 1.2f;
        [Tooltip("釣り上げに必要な基本秒数（魚のレベルで補正されます）")]
        [SerializeField] private float baseRequiredTime = 2.0f;
        [Tooltip("ルアーが離れたときのゲージ減少速度（秒あたり）")]
        [SerializeField] private float gaugeDecayRate = 0.5f;

        [Header("釣り上げ演出設定")]
        [Tooltip("釣り上げ時にピョンと跳ね上がる高さ")]
        [SerializeField] private float catchJumpHeight = 1.2f;
        [Tooltip("釣り上げ演出の所要時間（秒）")]
        [SerializeField] private float catchAnimDuration = 0.6f;
        [Tooltip("釣り上げ時に生成するパーティクルPrefab（任意）")]
        [SerializeField] private GameObject catchEffectPrefab;

        [Header("演出用イベント (インスペクター連携可能)")]
        [SerializeField] private UnityEvent onLureEnterEvent;
        [SerializeField] private UnityEvent onLureExitEvent;
        [SerializeField] private UnityEvent<float> onGaugeChangedEvent; // 0.0〜1.0の正規化値
        [SerializeField] private UnityEvent onCaughtEvent;

        // C# イベント（スクリプト側からの監視用）
        public event Action<FishMovement> OnCaught;
        public event Action<float, float> OnGaugeChanged; // (current, max)
        public event Action OnLureEnter;
        public event Action OnLureExit;

        // 状態プロパティ
        public FishStatus Status => fishStatus;
        public float CurrentGauge => currentGauge;
        public float RequiredTime => requiredTime;
        public float NormalizedGauge => requiredTime > 0 ? Mathf.Clamp01(currentGauge / requiredTime) : 0f;
        public bool IsCaught => isCaught;
        public bool IsLureNear => isLureNear;

        private Transform lureTransform;
        private SpriteRenderer spriteRenderer;
        private Vector2 targetPosition;
        private float waitTimer;
        private float currentGauge = 0f;
        private float requiredTime = 2f;
        private bool isCaught = false;
        private bool isLureNear = false;
        private Vector3 initialScale;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            initialScale = transform.localScale;
        }

        private void Update()
        {
            if (isCaught) return;

            UpdateMovement();
            UpdateCatchGauge();
        }

        /// <summary>
        /// 魚の初期化。既存の FishStatus データ構造を直接保持し、余計なデータの受け渡しを避けます。
        /// </summary>
        public void Initialize(FishStatus status, Collider2D area, Transform lure)
        {
            this.fishStatus = status;
            this.moveArea = area;
            this.lureTransform = lure;
            this.isCaught = false;
            this.currentGauge = 0f;
            transform.localScale = initialScale;

            // FishStatus からデータを反映
            if (fishStatus != null)
            {
                if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && fishStatus.fishImage != null)
                {
                    spriteRenderer.sprite = fishStatus.fishImage;
                }

                moveSpeed = fishStatus.fishSpeed > 0 ? fishStatus.fishSpeed : 1.5f;
                // レベルに応じた難易度補正（基本時間 + レベル * 0.3秒）
                requiredTime = Mathf.Max(1.0f, baseRequiredTime + (fishStatus.level * 0.3f));
            }
            else
            {
                requiredTime = baseRequiredTime;
            }

            // 指定エリア内のランダム位置に初期配置
            PickNewTargetPosition();
            transform.position = targetPosition;
        }

        /// <summary>
        /// 指定エリア内の回遊移動
        /// </summary>
        private void UpdateMovement()
        {
            if (moveArea == null) return;

            // 目標地点に到達したときの待機判定
            if (Vector2.Distance(transform.position, targetPosition) < 0.05f)
            {
                waitTimer += Time.deltaTime;
                if (waitTimer >= waitTime)
                {
                    waitTimer = 0f;
                    PickNewTargetPosition();
                }
                return;
            }

            // 向きに合わせてスプライトを左右反転
            if (spriteRenderer != null)
            {
                float directionX = targetPosition.x - transform.position.x;
                if (Mathf.Abs(directionX) > 0.01f)
                {
                    spriteRenderer.flipX = directionX < 0;
                }
            }

            // 目標地点へ移動
            transform.position = Vector2.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );
        }

        /// <summary>
        /// ルアー接近判定とゲージ計算
        /// </summary>
        private void UpdateCatchGauge()
        {
            if (lureTransform == null) return;

            float distance = Vector2.Distance(transform.position, lureTransform.position);

            if (distance <= catchDistance)
            {
                // ルアーが接近した瞬間の演出通知
                if (!isLureNear)
                {
                    isLureNear = true;
                    OnLureEnter?.Invoke();
                    onLureEnterEvent?.Invoke();
                }

                // ゲージ加算
                currentGauge += Time.deltaTime;
                NotifyGaugeChanged();

                // ゲージ満タンで釣り上げ
                if (currentGauge >= requiredTime)
                {
                    Catch();
                }
            }
            else
            {
                // ルアーが離れた瞬間の演出通知
                if (isLureNear)
                {
                    isLureNear = false;
                    OnLureExit?.Invoke();
                    onLureExitEvent?.Invoke();
                }

                // ゲージ徐々に減少
                if (currentGauge > 0f)
                {
                    currentGauge = Mathf.Max(0f, currentGauge - gaugeDecayRate * Time.deltaTime);
                    NotifyGaugeChanged();
                }
            }
        }

        private void NotifyGaugeChanged()
        {
            OnGaugeChanged?.Invoke(currentGauge, requiredTime);
            onGaugeChangedEvent?.Invoke(NormalizedGauge);
        }

        /// <summary>
        /// 魚を釣り上げたときの処理（跳ね上がり演出後に非表示）
        /// </summary>
        public void Catch()
        {
            if (isCaught) return;

            isCaught = true;
            currentGauge = requiredTime;
            NotifyGaugeChanged();

            // 演出・通知を発火
            OnCaught?.Invoke(this);
            onCaughtEvent?.Invoke();

            // 簡易跳ね上がり演出コルーチン開始
            StartCoroutine(CatchAnimationCoroutine());
        }

        /// <summary>
        /// ピョンと跳ね上がってフェード・縮小する簡易演出
        /// </summary>
        private IEnumerator CatchAnimationCoroutine()
        {
            Vector3 startPos = transform.position;
            Vector3 originalScale = transform.localScale;
            float elapsed = 0f;

            // 任意エフェクトの生成
            if (catchEffectPrefab != null)
            {
                Instantiate(catchEffectPrefab, transform.position, Quaternion.identity);
            }

            while (elapsed < catchAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / catchAnimDuration);

                // 山型放物線カーブで跳ね上がり（4 * t * (1 - t)）
                float jumpOffset = 4f * t * (1f - t) * catchJumpHeight;
                transform.position = startPos + new Vector3(0f, jumpOffset, 0f);

                // 後半に少し拡大してからシュッと縮んで消える演出
                if (t < 0.3f)
                {
                    float popT = t / 0.3f;
                    transform.localScale = Vector3.Lerp(originalScale, originalScale * 1.25f, popT);
                }
                else
                {
                    float shrinkT = (t - 0.3f) / 0.7f;
                    transform.localScale = Vector3.Lerp(originalScale * 1.25f, Vector3.zero, shrinkT);
                }

                yield return null;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 指定エリアの境界内から新しい目的地をランダムに決定
        /// </summary>
        private void PickNewTargetPosition()
        {
            if (moveArea == null) return;

            Bounds bounds = moveArea.bounds;
            float x = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
            float y = UnityEngine.Random.Range(bounds.min.y, bounds.max.y);
            targetPosition = new Vector2(x, y);
        }

        private void OnDrawGizmosSelected()
        {
            // インスペクターで選択中に接近判定サークルを可視化
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, catchDistance);
        }
    }
}
