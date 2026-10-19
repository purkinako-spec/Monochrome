using UnityEngine;
using Runtime.Dialogue.Logic;

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
        
        [Tooltip("開始ノードID (空なら Container のデフォルト)")]
        [SerializeField] private string _startNodeID = "";

        [Header("Interaction")]
        [Tooltip("表示用テキスト (例: '話しかける', '調べる')")]
        [SerializeField] private string _interactionText = "話しかける";

        [Header("Optional")]
        [Tooltip("会話終了後にオブジェクトを破棄するか (テスト用など)")]
        [SerializeField] private bool _destroyAfterDialogue = false;

        public string GetInteractionText() => _interactionText;

        public void Interact(GameObject interactor)
        {
            if (_dialogueContainer == null)
            {
                Debug.LogWarning($"[{name}] DialogueContainer が未設定です。", this);
                return;
            }

            var manager = DialogueManager.Instance;
            if (manager == null)
            {
                Debug.LogError("DialogueManager.Instance が見つかりません。シーンに配置されているか確認してください。", this);
                return;
            }

            // View の登録は Interact.cs (Player側) の Awake で完了している想定。
            // ここでは Manager に View があるかのみチェックし、なければエラーにする（設計上のミスを検知しやすくするため）。
            if (manager.CurrentView == null)
            {
                Debug.LogError("DialogueManager に View が登録されていません。Player に Interact コンポーネントがアタッチされ、DialogueViewProvider が設定されているか確認してください。", this);
                return;
            }

            // 開始ノードIDを指定して会話開始 (DialogueManager の新オーバーロードを使用)
            manager.StartDialogue(_dialogueContainer, _startNodeID, OnDialogueEnded);
        }

        /// <summary>
        /// 会話終了時のコールバック
        /// </summary>
        private void OnDialogueEnded()
        {
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
