using UnityEngine;
using Runtime.Dialogue.Core; // DialogueManager, DialogueContainer
using Runtime.Dialogue.Logic;
public class DialogueStarter : MonoBehaviour
{
    [SerializeField] private DialogueContainer dialogueData;
    public void PlayDialogue()
    {
        if (dialogueData != null && DialogueManager.Instance != null)
        {
            // 会話を開始（第2引数で終了時コールバックも設定可能）
            DialogueManager.Instance.StartDialogue(dialogueData, () =>
            {
                Debug.Log("会話が終了しました");
            });
        }
    }
}
