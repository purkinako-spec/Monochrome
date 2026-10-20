using UnityEngine;
using Runtime.Dialogue.Interaction;

public class NPCInteractable : MonoBehaviour, IInteractableProcessor
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
