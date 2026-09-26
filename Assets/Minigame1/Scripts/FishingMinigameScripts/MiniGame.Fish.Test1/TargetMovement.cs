using TMPro;
using UnityEngine;

namespace MiniGame.Fish.Test1
{
    public class TargetMovement : MonoBehaviour
    {
        [SerializeField] private Collider2D moveArea;

        [Header("à⁄ìÆê›íË")]
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float waitTime = 1f;

        private Vector2 targetPosition;
        private float waitTimer;

        void Start()
        {
            GetMovePosition();
        }

        // Update is called once per frame
        void Update()
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

        private void GetMovePosition()
        {
            Bounds bounds = moveArea.bounds;

            float x = Random.Range(bounds.min.x, bounds.max.x);
            float y = Random.Range(bounds.min.y, bounds.max.y);

            targetPosition = new Vector2(x, y);
        }
    }
}