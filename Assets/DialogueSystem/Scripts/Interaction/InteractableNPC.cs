using UnityEngine;
using Runtime.Dialogue.Logic;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Interaction
{
    /// <summary>
    /// 会話可能な NPC / オブジェクト
    /// IInteractable を実装し、インタラクト時に DialogueTesting と同様の手順で会話を開始する
    /// </summary>
    public class InteractableNPC : MonoBehaviour, IInteractable
    {
        [Header("Dialogue Data")]
        [Tooltip("会話データコンテナ (DialogueTesting.testData と同じ型)")]
        [SerializeField] private DialogueContainer _dialogueContainer;
        
        //[Tooltip("開始ノードID (空なら Container のデフォルト)")]
        //[SerializeField] private string _startNodeID = "";

        [Header("Interaction")]
        [Tooltip("表示用テキスト (例: '話しかける', '調べる')")]
        [SerializeField] private string _interactionText = "話しかける";

        [Header("Optional")]
        [Tooltip("会話終了後にオブジェクトを破棄するか (テスト用など)")]
        [SerializeField] private bool _destroyAfterDialogue = false;

        private bool _isDialogueActive = false;

        public string GetInteractionText() => _interactionText;

        public void Interact(GameObject interactor)
        {
            if (_isDialogueActive)
            {
                Debug.Log($"[{name}] 会話中のためスキップ");
                return;
            }

            if (_dialogueContainer == null)
            {
                Debug.LogWarning($"[{name}] DialogueContainer が未設定です。", this);
                return;
            }

            var manager = DialogueManager.Instance;
            if (manager == null)
            {
                Debug.LogError("DialogueManager.Instance が見つかりません。シーンに DialogueManager が配置されているか確認してください。", this);
                return;
            }

            // View の登録待機 (最大1秒)
            if (manager.CurrentView == null)
            {
                Debug.LogWarning("DialogueManager に View が未登録です。1秒待機してリトライします。");
                StartCoroutine(WaitForViewAndStartDialogue(manager));
                return;
            }

            StartDialogueInternal(manager);
        }

        private System.Collections.IEnumerator WaitForViewAndStartDialogue(DialogueManager manager)
        {
            float timer = 0f;
            while (manager.CurrentView == null && timer < 1f)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (manager.CurrentView != null)
            {
                StartDialogueInternal(manager);
            }
            else
            {
                Debug.LogError("DialogueManager に View が登録されませんでした。Player に Interact コンポーネントと DialogueViewProvider が正しく設定されているか確認してください。", this);
            }
        }

        private void StartDialogueInternal(DialogueManager manager)
        {
            _isDialogueActive = true;
            Debug.Log($"[{name}] 会話開始: {_dialogueContainer.name}");
            
            // 会話開始 (終了コールバックで _isDialogueActive を false に戻す)
            manager.StartDialogue(_dialogueContainer, OnDialogueEnded);
        }

        /// <summary>
        /// 会話終了時のコールバック
        /// </summary>
        private void OnDialogueEnded()
        {
            _isDialogueActive = false;
            Debug.Log($"[{name}] 会話が終了しました。");

            if (_destroyAfterDialogue)
            {
                Destroy(gameObject);
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_interactionText)) _interactionText = "インタラクト";
        }
    }
}
