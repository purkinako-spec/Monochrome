using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniGame.Fish.Test1
{
    public class LureMovement : MonoBehaviour
    {
        [SerializeField] private float speed = 5f;
        private Vector2 moveInput;
        private bool canInput = false;

        // 食いついているときの設定
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float waitTime = 1f;

        private Vector2 targetPosition;
        private float waitTimer;

        // Player Inputコンポーネントから呼ばれるメソッド
        public void OnMove(InputAction.CallbackContext context)
        {
            // WASDや矢印キーの入力をVector2（X軸, Y軸）として読み込む
            moveInput = context.ReadValue<Vector2>();
        }

        void Update()
        {
            if (canInput)
            {

                Vector3 movement = new Vector3(moveInput.x, moveInput.y, 0f) * speed * Time.deltaTime;
                transform.Translate(movement);

            }
            else
            {
                if (Vector2.Distance(transform.position, targetPosition) < 0.05f)
                {
                    waitTimer += Time.deltaTime;

                    if (waitTimer >= waitTime)
                    {
                        waitTimer = 0f;
                        GetMovePosition();
                    }
                    return;
                }

                transform.position = Vector2.MoveTowards(
                    transform.position,
                    targetPosition,
                    moveSpeed * Time.deltaTime
                );
            }
        }

        [SerializeField] private Collider2D moveArea;
        private void GetMovePosition()
        {
            Bounds bounds = moveArea.bounds;

            float x = Random.Range(bounds.min.x, bounds.max.x);
            float y = 0;

            targetPosition = new Vector2(x, y);
        }

        public void ChantaCanInput(bool state)
        {
            canInput = state;
        }
    }
}