using UnityEngine;

public class InteractionController : MonoBehaviour
{
    [SerializeField] private float interactDistance = 1.5f;
    [SerializeField] private LayerMask interactableLayer;

    private IInteractable currentInteractable;

    private void Update()
    {
        FindInteractable();

        if (Input.GetKeyDown(KeyCode.E))
        {
            Interact();
        }
    }

    private void FindInteractable()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            interactDistance,
            interactableLayer
        );

        currentInteractable = null;

        float closestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            IInteractable interactable =
                hit.GetComponent<IInteractable>();

            if (interactable == null)
                continue;

            float distance =
                Vector2.Distance(transform.position, hit.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                currentInteractable = interactable;
            }
        }
    }

    private void Interact()
    {
        if (currentInteractable == null)
            return;

        currentInteractable.Interact(gameObject);
    }
}