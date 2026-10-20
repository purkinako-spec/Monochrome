using UnityEngine;
using Runtime.Dialogue.Interaction;
using MiniGame.Fish.Test2;

public class NPCInteractable : MonoBehaviour, IInteractableProcessor
{
    [SerializeField] private string interactionText = "話す";
    [SerializeField] private int priority = 0;
    [SerializeField] private string sceneName = "FishingScene"; // 遷移先シーン名

    public string GetInteractionText()
    {
        return interactionText;
    }

    public bool CanInteract(GameObject interactor)
    {
        // ここにインタラクション可能かどうかの判定ロジックを追加
        return true;
    }

    public int Priority => priority;

    public void Interact(GameObject interactor)
    {
        Debug.Log("NPCとの会話開始");
        SceneLoader sceneLoader = GetComponent<SceneLoader>();
        if (sceneLoader != null)
        {
            sceneLoader.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError("SceneLoader コンポーネントが見つかりません。", this);
        }
    }
}
