using UnityEngine;
using System.Collections;
using Runtime.Dialogue.Logic;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Interaction;

namespace Runtime.Dialogue.Interaction.Processors
{
    /// <summary>
    /// Script 3 実装: 会話開始処理
    /// </summary>
    [AddComponentMenu("Interaction/Processors/Dialogue Processor")]
    public class DialogueProcessor : MonoBehaviour, IInteractableProcessor
    {
        [Header("Dialogue Data")]
        [SerializeField] private DialogueContainer _dialogueContainer;
        [SerializeField] private string _interactionText = "話しかける";
        [SerializeField] private int _priority = 0;
        [SerializeField] private bool _destroyAfterDialogue = false;

        private bool _isDialogueActive = false;

        // --- IInteractableProcessor 実装 ---
        public bool CanInteract(GameObject interactor) => !_isDialogueActive && _dialogueContainer != null;
        
        public string GetInteractionText() => _interactionText;
        
        public int Priority 
        { 
            get => _priority; 
            set => _priority = value; 
        }

        // ★ NPCInteractable から設定できるよう public setter を追加
        public DialogueContainer DialogueContainer 
        { 
            get => _dialogueContainer; 
            set => _dialogueContainer = value; 
        }
        
        public string InteractionText 
        { 
            get => _interactionText; 
            set => _interactionText = value; 
        }
        
        public bool DestroyAfterDialogue 
        { 
            get => _destroyAfterDialogue; 
            set => _destroyAfterDialogue = value; 
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            var manager = DialogueManager.Instance;
            if (manager == null)
            {
                Debug.LogError("DialogueManager.Instance が見つかりません。シーンに DialogueManager が配置されているか確認してください。", this);
                return;
            }

            // View の登録待機 (最大1秒) - 既存 InteractableNPC のロジックを踏襲
            if (manager.CurrentView == null)
            {
                StartCoroutine(WaitForViewAndStart(manager));
                return;
            }

            StartDialogueInternal(manager);
        }

        private IEnumerator WaitForViewAndStart(DialogueManager manager)
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
            if (string.IsNullOrEmpty(_interactionText)) _interactionText = "話しかける";
        }
    }
}
