using UnityEngine;
using Runtime.Dialogue.Interaction;
using Runtime.Dialogue.Interaction.Processors;
using Runtime.Dialogue.Core;

namespace MiniGame.Fish.Test2.Interaction
{
    /// <summary>
    /// NPC会話用のセットアップヘルパーコンポーネント
    /// InteractableReceiver と DialogueProcessor を自動で構成し、インスペクタで会話データを設定できるようにする
    /// 自身も IInteractableProcessor を実装し、処理を内部の DialogueProcessor に委譲する
    /// </summary>
    [RequireComponent(typeof(InteractableReceiver))]
    [RequireComponent(typeof(DialogueProcessor))]
    [AddComponentMenu("Interaction/NPC Interactable")]
    public class NPCInteractable : MonoBehaviour, IInteractableProcessor
    {
        [Header("Dialogue Settings")]
        [Tooltip("会話データコンテナ")]
        [SerializeField] private DialogueContainer _dialogueContainer;
        
        [Tooltip("表示用テキスト (例: '話しかける', '相談する')")]
        [SerializeField] private string _interactionText = "話しかける";

        [Tooltip("優先度 (他のインタラクトと重なった場合の優先順位)")]
        [SerializeField] private int _priority = 0;

        [Tooltip("会話終了後にこのオブジェクトを破棄するか")]
        [SerializeField] private bool _destroyAfterDialogue = false;

        // 内部参照
        private InteractableReceiver _receiver;
        private DialogueProcessor _processor;

        private void Awake()
        {
            _receiver = GetComponent<InteractableReceiver>();
            _processor = GetComponent<DialogueProcessor>();
            
            // 起動時にも同期 (Resetで設定されていない場合やプレハブ上書き時の保険)
            SyncSettingsToProcessor();
        }

        private void OnValidate()
        {
            // インスペクタで値変更時に即時反映
            SyncSettingsToProcessor();
        }

        private void Reset()
        {
            // コンポーネント追加時に自動で参照取得・同期
            _receiver = GetComponent<InteractableReceiver>();
            _processor = GetComponent<DialogueProcessor>();
            SyncSettingsToProcessor();
        }

        /// <summary>
        /// このコンポーネントの設定を DialogueProcessor へ反映し、Receiver に登録する
        /// </summary>
        private void SyncSettingsToProcessor()
        {
            if (_processor == null) return;

            _processor.DialogueContainer = _dialogueContainer;
            _processor.InteractionText = _interactionText;
            _processor.Priority = _priority;
            _processor.DestroyAfterDialogue = _destroyAfterDialogue;

            // Receiver の Processor フィールドが未設定なら自動でセット (SerializeReference対応)
            if (_receiver != null && _receiver.Processor == null)
            {
                _receiver.Processor = _processor;
            }
        }

        /// <summary>
        /// 外部から会話データを動的に差し替える場合用
        /// </summary>
        public void SetDialogueContainer(DialogueContainer container)
        {
            _dialogueContainer = container;
            SyncSettingsToProcessor();
        }

        // ============================================================
        // IInteractableProcessor 実装 (内部 Processor に委譲)
        // ============================================================

        public bool CanInteract(GameObject interactor) 
            => _processor != null && _processor.CanInteract(interactor);

        public void Interact(GameObject interactor) 
            => _processor?.Interact(interactor);

        public string GetInteractionText() 
            => _processor?.GetInteractionText() ?? _interactionText;

        public int Priority 
            => _processor?.Priority ?? _priority;
    }
}
