using System.Collections;
using UnityEngine;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic; // または Runtime.Dialogue (DialogueViewWindowのnamespace)
using Runtime.Dialogue;

public class DialogueStarter : MonoBehaviour
{
    [SerializeField] private DialogueContainer dialogueData;
    [SerializeField] private DialogueViewWindow dialogueView; // InspectorからDialogueCanvas内のWindowをアタッチ
    public void PlayDialogue()
    {
        if (dialogueData == null || DialogueManager.Instance == null)
        {
            Debug.LogError("DialogueData または DialogueManager が見つかりません。");
            return;
        }
        // Viewを明示的に登録（Awake順序や非アクティブによる取りこぼしを防止）
        if (dialogueView != null)
        {
            DialogueManager.Instance.RegisterView(dialogueView);
        }
        DialogueManager.Instance.StartDialogue(dialogueData, () =>
        {
            Debug.Log("会話が終了しました");
        });
    }
}
