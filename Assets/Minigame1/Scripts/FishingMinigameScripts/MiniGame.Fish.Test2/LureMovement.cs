using UnityEngine;
using UnityEngine.InputSystem;
using MiniGame.Fish.Test1;

namespace MiniGame.Fish.Test2
{
    /// <summary>
    /// ルアーの移動制御スクリプト
    /// 新InputSystemのイベント受信および直接キー入力に対応しています。
    /// </summary>
    public class LureMovement : MonoBehaviour
    {
        [Header("移動設定")]
        [SerializeField] private float speed = 5f;
        [SerializeField] private bool canInput = true;

        [Header("移動可能範囲 (任意設定)")]
        [Tooltip("設定するとルアーの移動範囲をこのCollider2D内に制限します")]
        [SerializeField] private Collider2D movableArea;

        private Vector2 moveInput;
        private Rigidbody2D rb;

        public Vector2 Position => transform.position;
        public bool CanInput => canInput;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        /// <summary>
        /// PlayerInputコンポーネントからのMoveアクション受信
        /// </summary>
        public void OnMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        private void Update()
        {
            if (!canInput) return;

            // 新InputSystemのPlayerInput未アタッチ時のフォールバック (キーボードWASD/矢印キー)
            Vector2 input = moveInput;
            if (input == Vector2.zero)
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                input = new Vector2(h, v).normalized;
            }

            // 移動計算
            Vector3 movement = new Vector3(input.x, input.y, 0f) * speed * Time.deltaTime;
            Vector3 newPosition = transform.position + movement;

            // 移動範囲制限（Collider2Dが指定されている場合）
            if (movableArea != null)
            {
                Bounds bounds = movableArea.bounds;
                newPosition.x = Mathf.Clamp(newPosition.x, bounds.min.x, bounds.max.x);
                newPosition.y = Mathf.Clamp(newPosition.y, bounds.min.y, bounds.max.y);
            }

            transform.position = newPosition;
        }

        /// <summary>
        /// 操作受付の有効/無効切り替え
        /// </summary>
        public void SetCanInput(bool state)
        {
            canInput = state;
            if (!state)
            {
                moveInput = Vector2.zero;
            }
        }
    }
}
