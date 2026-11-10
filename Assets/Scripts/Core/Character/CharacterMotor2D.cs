using System;
using UnityEngine;

namespace Monochrome.Core.Character
{
    public enum MovementStyle
    {
        Walk,
        Run,
        Sneak
    }

    /// <summary>
    /// プレイヤーおよびNPC共通の2D物理移動モーター。
    /// Rigidbody2D を制御し、速度・移動スタイル・4方向の向き・ワープを統括します。
    /// 人間の入力（PlayerController2D）または自動移動（CharacterNavigator2D）から入力を受け取ります。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [AddComponentMenu("Monochrome/Character/Character Motor 2D")]
    public class CharacterMotor2D : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float runSpeed = 5.5f;
        [SerializeField] private float sneakSpeed = 1.8f;

        [Header("Initial Facing")]
        [SerializeField] private Vector2 initialFacing = Vector2.down;

        [Header("Optional References")]
        [Tooltip("アニメーターがアタッチされている場合はパラメータ(MoveX, MoveY, Speed, FacingX, FacingY)を自動更新します")]
        [SerializeField] private Animator animator;

        // 内部コンポーネント
        private Rigidbody2D rb;

        // 状態
        private Vector2 movementInput = Vector2.zero;
        private MovementStyle currentStyle = MovementStyle.Walk;
        private Vector2 facingDirection = Vector2.down;
        private bool isFacingLocked = false;

        // アニメーション用ハッシュ
        private static readonly int HashMoveX = Animator.StringToHash("MoveX");
        private static readonly int HashMoveY = Animator.StringToHash("MoveY");
        private static readonly int HashSpeed = Animator.StringToHash("Speed");
        private static readonly int HashFacingX = Animator.StringToHash("FacingX");
        private static readonly int HashFacingY = Animator.StringToHash("FacingY");

        // イベント
        public event Action<Vector2> OnFacingDirectionChanged;
        public event Action<Vector2> OnMovementChanged;

        public Vector2 FacingDirection => facingDirection;
        public MovementStyle CurrentStyle => currentStyle;
        public bool IsMoving => movementInput.sqrMagnitude > 0.001f;
        public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;

        public float CurrentSpeed => currentStyle switch
        {
            MovementStyle.Walk => walkSpeed,
            MovementStyle.Run => runSpeed,
            MovementStyle.Sneak => sneakSpeed,
            _ => walkSpeed
        };

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            SetFacingDirection(initialFacing != Vector2.zero ? initialFacing : Vector2.down);
        }

        private void FixedUpdate()
        {
            ApplyMovement();
        }

        private void ApplyMovement()
        {
            if (rb == null || rb.bodyType == RigidbodyType2D.Static) return;

            if (movementInput.sqrMagnitude > 0.001f)
            {
                Vector2 targetVelocity = movementInput.normalized * CurrentSpeed;
                rb.linearVelocity = targetVelocity;
                OnMovementChanged?.Invoke(targetVelocity);

                if (!isFacingLocked)
                {
                    UpdateFacingDirection(movementInput);
                }
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
                OnMovementChanged?.Invoke(Vector2.zero);
            }

            UpdateAnimator();
        }

        /// <summary>
        /// 移動入力を設定します（外部のプレイヤー入力やAIナビゲーターから毎フレーム呼び出します）
        /// </summary>
        public void SetMovementInput(Vector2 input)
        {
            movementInput = Vector2.ClampMagnitude(input, 1f);
        }

        /// <summary>
        /// 移動スタイル（歩き・走り・忍び足）を切り替えます
        /// </summary>
        public void SetMovementStyle(MovementStyle style)
        {
            currentStyle = style;
        }

        /// <summary>
        /// 向きを直接指定します（上、下、左、右）
        /// </summary>
        public void SetFacingDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;

            // 4方向にスナップ（水平または垂直で大きい方を優先）
            Vector2 snapped = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                ? new Vector2(Mathf.Sign(direction.x), 0)
                : new Vector2(0, Mathf.Sign(direction.y));

            if (facingDirection != snapped)
            {
                facingDirection = snapped;
                OnFacingDirectionChanged?.Invoke(facingDirection);
            }
        }

        private void UpdateFacingDirection(Vector2 direction)
        {
            SetFacingDirection(direction);
        }

        /// <summary>
        /// 向きの固定（前を向いたまま後ずさりする等の演出用）を設定・解除します
        /// </summary>
        public void SetFacingLocked(bool locked)
        {
            isFacingLocked = locked;
        }

        /// <summary>
        /// 物理的な動きを即座に完全停止させます
        /// </summary>
        public void StopImmediately()
        {
            movementInput = Vector2.zero;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            UpdateAnimator();
        }

        /// <summary>
        /// 指定座標と向きへ即座にワープ（テレポート）させます。
        /// エリア移動（スポーン地点への出現）で使用します。
        /// </summary>
        public void WarpTo(Vector2 position, Vector2? newFacing = null)
        {
            StopImmediately();
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            if (rb != null)
            {
                rb.position = position;
                rb.linearVelocity = Vector2.zero;
            }

            if (newFacing.HasValue)
            {
                SetFacingDirection(newFacing.Value);
            }
            UpdateAnimator();
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            float speedValue = movementInput.magnitude * (currentStyle == MovementStyle.Run ? 2f : 1f);
            animator.SetFloat(HashSpeed, speedValue);
            animator.SetFloat(HashMoveX, movementInput.x);
            animator.SetFloat(HashMoveY, movementInput.y);
            animator.SetFloat(HashFacingX, facingDirection.x);
            animator.SetFloat(HashFacingY, facingDirection.y);
        }
    }
}
