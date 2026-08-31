using MiniGame.Fish;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public class FishMovement : MonoBehaviour
{
    [SerializeField] private Collider2D moveArea;

    [Header("à⁄ìÆê›íË")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float waitTime = 1f;

    private Vector2 targetPosition;
    private float waitTimer;

    private FishStatus myStatus;

    [SerializeField] private bool wait = true;

    [SerializeField] private GameObject lureObject;

    private void Start()
    {
        GetMovePosition();
    }

    private void Update()
    {
        if (wait)
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
        else
        {
            transform.position = lureObject.transform.position;
        }
    }

    private void GetMovePosition()
    {
        Bounds bounds = moveArea.bounds;

        float x = Random.Range(bounds.min.x, bounds.max.x);
        float y = Random.Range(bounds.min.y, bounds.max.y);

        targetPosition = new Vector2(x, y);
    }

    public void SetMoveArea(Collider2D moveArea)
    {
        this.moveArea = moveArea;
    }

    public void SetStatus(FishStatus status)
    {
        myStatus = status;
        GetComponent<SpriteRenderer>().sprite = myStatus.fishImage;
        moveSpeed = status.fishSpeed;

        GetMovePosition();
        transform.position = targetPosition;
    }

    public void ChangeWait(bool state)
    {
        wait = state;
    }
}
