using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Monochrome.Core.Character
{
    public enum NavigationMode
    {
        Idle,
        MoveToTarget,
        Patrol,
        Wander
    }

    /// <summary>
    /// キャラクター（NPCおよびカットシーン中のプレイヤー）を自動移動させるナビゲーター。
    /// CharacterMotor2D と連携し、指定位置への自動歩行、巡回パトロール、徘徊、
    /// および会話時に立ち止まってプレイヤーを向く制御を行います。
    /// </summary>
    [RequireComponent(typeof(CharacterMotor2D))]
    [AddComponentMenu("Monochrome/Character/Character Navigator 2D")]
    public class CharacterNavigator2D : MonoBehaviour
    {
        [Header("Mode Settings")]
        [SerializeField] private NavigationMode defaultMode = NavigationMode.Idle;

        [Header("Patrol Settings")]
        [Tooltip("巡回するウェイポイント（空の場合は自身の初期位置）")]
        [SerializeField] private List<Transform> patrolWaypoints = new List<Transform>();
        [SerializeField] private float waypointWaitDuration = 2.0f;
        [SerializeField] private MovementStyle patrolStyle = MovementStyle.Walk;

        [Header("Wander Settings")]
        [Tooltip("初期位置からランダム徘徊する半径")]
        [SerializeField] private float wanderRadius = 3.0f;
        [SerializeField] private float minWanderWait = 2.0f;
        [SerializeField] private float maxWanderWait = 5.0f;
        [SerializeField] private MovementStyle wanderStyle = MovementStyle.Walk;

        [Header("Safety & Tolerance")]
        [SerializeField] private float stoppingDistance = 0.1f;
        [SerializeField] private float defaultTimeout = 8.0f;

        // コンポーネント参照
        private CharacterMotor2D motor;

        // 状態管理
        private NavigationMode currentMode = NavigationMode.Idle;
        private Vector2 originPosition;
        private Coroutine activeRoutine;
        private bool isPausedByInteraction = false;
        private Vector2 preInteractionFacing;

        public NavigationMode CurrentMode => currentMode;
        public bool IsNavigating => currentMode != NavigationMode.Idle;

        private void Awake()
        {
            motor = GetComponent<CharacterMotor2D>();
            originPosition = transform.position;
        }

        private void Start()
        {
            SetMode(defaultMode);
        }

        private void OnDisable()
        {
            StopCurrentTask();
        }

        /// <summary>
        /// 動作モードを切り替えます
        /// </summary>
        public void SetMode(NavigationMode mode)
        {
            StopCurrentTask();
            currentMode = mode;

            switch (mode)
            {
                case NavigationMode.Patrol:
                    activeRoutine = StartCoroutine(PatrolRoutine());
                    break;
                case NavigationMode.Wander:
                    activeRoutine = StartCoroutine(WanderRoutine());
                    break;
                case NavigationMode.Idle:
                default:
                    motor.StopImmediately();
                    break;
            }
        }

        // ==========================================
        // 🎯 指定位置への移動（非同期 / イベント演出用）
        // ==========================================

        /// <summary>
        /// 指定座標へ歩行移動させます。到着するかタイムアウトするまで非同期で待機します。
        /// </summary>
        public async Task<bool> MoveToAsync(Vector2 destination, MovementStyle style = MovementStyle.Walk, float? timeoutSeconds = null)
        {
            StopCurrentTask();
            currentMode = NavigationMode.MoveToTarget;

            var tcs = new TaskCompletionSource<bool>();
            activeRoutine = StartCoroutine(MoveToPositionRoutine(destination, style, timeoutSeconds ?? defaultTimeout, tcs));
            return await tcs.Task;
        }

        private IEnumerator MoveToPositionRoutine(Vector2 target, MovementStyle style, float timeout, TaskCompletionSource<bool> tcs)
        {
            motor.SetMovementStyle(style);
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (isPausedByInteraction)
                {
                    motor.SetMovementInput(Vector2.zero);
                    yield return null;
                    continue;
                }

                Vector2 currentPos = transform.position;
                Vector2 diff = target - currentPos;

                // 到着判定
                if (diff.sqrMagnitude <= stoppingDistance * stoppingDistance)
                {
                    motor.StopImmediately();
                    currentMode = NavigationMode.Idle;
                    tcs.TrySetResult(true);
                    yield break;
                }

                // 目的地へ向かって入力を設定
                motor.SetMovementInput(diff.normalized);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // タイムアウト（スタック防止）
            Debug.LogWarning($"[CharacterNavigator2D] '{gameObject.name}' の移動がタイムアウトしました。強制停止します。", this);
            motor.StopImmediately();
            currentMode = NavigationMode.Idle;
            tcs.TrySetResult(false);
        }

        // ==========================================
        // 🚶 パトロール（巡回）ルーチン
        // ==========================================

        private IEnumerator PatrolRoutine()
        {
            if (patrolWaypoints == null || patrolWaypoints.Count == 0)
            {
                yield break;
            }

            int index = 0;
            while (true)
            {
                var wp = patrolWaypoints[index];
                if (wp != null)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    yield return StartCoroutine(MoveToPositionRoutine(wp.position, patrolStyle, defaultTimeout, tcs));

                    // 到着後の待機
                    motor.StopImmediately();
                    yield return new WaitForSeconds(waypointWaitDuration);
                }

                index = (index + 1) % patrolWaypoints.Count;
            }
        }

        // ==========================================
        // 🐾 ランダム徘徊ルーチン
        // ==========================================

        private IEnumerator WanderRoutine()
        {
            while (true)
            {
                // 原点周辺のランダム地点を算出
                Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * wanderRadius;
                Vector2 destination = originPosition + randomOffset;

                var tcs = new TaskCompletionSource<bool>();
                yield return StartCoroutine(MoveToPositionRoutine(destination, wanderStyle, defaultTimeout, tcs));

                // 立ち止まって休憩
                motor.StopImmediately();
                float wait = UnityEngine.Random.Range(minWanderWait, maxWanderWait);
                yield return new WaitForSeconds(wait);
            }
        }

        // ==========================================
        // 💬 会話時の一時停止 ＆ 振り向き制御
        // ==========================================

        /// <summary>
        /// プレイヤーから話しかけられた時に呼び出され、パトロールを一時停止して話者の方向を向きます。
        /// </summary>
        public void PauseAndFace(Vector2 targetPosition)
        {
            isPausedByInteraction = true;
            preInteractionFacing = motor.FacingDirection;
            motor.StopImmediately();

            Vector2 lookDir = targetPosition - (Vector2)transform.position;
            motor.SetFacingDirection(lookDir);
        }

        /// <summary>
        /// 会話終了時に呼び出され、一時停止を解除して元の動作に復帰します。
        /// </summary>
        public void ResumeNavigation(bool restoreFacing = false)
        {
            if (restoreFacing)
            {
                motor.SetFacingDirection(preInteractionFacing);
            }
            isPausedByInteraction = false;
        }

        public void StopCurrentTask()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }
            if (motor != null)
            {
                motor.StopImmediately();
            }
            currentMode = NavigationMode.Idle;
        }
    }
}
