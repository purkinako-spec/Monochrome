using UnityEngine;

public class NPCInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionText = "話す";

    public string GetInteractionText()
    {
        return interactionText;
    }

    public void Interact(GameObject interactor)
    {
        Debug.Log("NPCとの会話開始");
        SceneLoader sceneLoader = GetComponent<SceneLoader>();
        sceneLoader.LoadScene();
    }
}