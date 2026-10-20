using UnityEngine;
using Runtime.Dialogue.Interaction;
using Runtime.Dialogue.Interaction.Processors;
using Runtime.Dialogue.Core;

namespace MiniGame.Fish.Test2.Interaction
{
    /// <summary>
    /// NPC会話用のセットアップヘルパーコンポーネント
    /// InteractableReceiver と DialogueProcessor を自動で構成し、インスペクタで会話データを設定できるようにする
    /// ※ 自身は IInteractableProcessor を実装しない (処理は DialogueProcessor に委譲)
    /// </summary>
    [RequireComponent(typeof(InteractableReceiver))]
    [RequireComponent(typeof(DialogueProcessor))]
    [AddComponentMenu("Interaction/NPC Interactable")]
    public class NPCInteractable : MonoBehaviour
    {
        [Header("Dialogue Settings")]
        [Tooltip("会話データコンテナ")]
        [SerializeField] private DialogueContainer _dialogueContainer;
        
        [Tooltip("表示用テキスト (例: '話しかける', '相談する')")]
        [SerializeField] private string _interactionText = "話しかける";

        [Tooltip("優先度 (他のインタラクトと重なった場合の優先順位)")]
        [SerializeField] private int _priority = 10;

        [Tooltip("会話終了後にこのオブジェクトを破棄するか")]
        [SerializeField] private bool _destroyAfterDialogue = false;

        // 内部参照
        private InteractableReceiver _receiver;
        private DialogueProcessor _processor;

        private void Awake()
        {
            _receiver = GetComponent<InteractableReceiver>();
            _processor = GetComponent<DialogueProcessor>();
            
            // 起動時に強制同期 (Receiver -> DialogueProcessor への接続を保証)
            SyncSettingsToProcessor();
        }

        private void OnValidate()
        {
            // インスペクタで値変更時に即時反映
            if (_processor == null) _processor = GetComponent<DialogueProcessor>();
            if (_receiver == null) _receiver = GetComponent<InteractableReceiver>();
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
            if (_processor == null || _receiver == null) return;

            // DialogueProcessor へ設定転送
            _processor.DialogueContainer = _dialogueContainer;
            _processor.InteractionText = _interactionText;
            _processor.Priority = _priority;
            _processor.DestroyAfterDialogue = _destroyAfterDialogue;

            // Receiver の Processor フィールドに DialogueProcessor を強制セット
            // (InteractableReceiver.Awake より確実に、かつ NPCInteractable 自身がインターフェース実装をやめたため競合しない)
            if (_receiver.Processor != _processor)
            {
                _receiver.Processor = _processor;
                Debug.Log($"[{name}] Receiver.Processor を DialogueProcessor に接続しました", this);
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
    }
}
