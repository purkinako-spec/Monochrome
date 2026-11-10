using System;
using UnityEngine;
using InteractionSystem2D;
using Monochrome.Core.Input;
using Runtime.Dialogue.Logic;

namespace Monochrome.Core.Transition
{
    public enum DoorTriggerType
    {
        IntraScene, // 同一シーン内ワープ
        InterScene  // 別シーンへのロード
    }

    public enum DoorActivationMode
    {
        InteractOnly,   // [E] / (A) ボタンを押して入る
        TouchOnly,      // 触れたら自動で移動（マップ端・階段等）
        Both            // どちらでも移動可能
    }

    /// <summary>
    /// エリア移動を行うドア・通路・境界コライダーコンポーネント。
    /// - 既存の IInteractable2D を実装し、PlayerInteractor2D と完全連携
    /// - ボタン押し移動（ドア）と接触自動移動（マップ端）の両対応
    /// - FlagManager と直結した鍵・イベント条件判定（未達成時はロック演出）
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Monochrome/Transition/Area Door 2D")]
    public class AreaDoor2D : MonoBehaviour, IInteractable2D
    {
        [Header("Transition Destination")]
        [SerializeField] private DoorTriggerType triggerType = DoorTriggerType.IntraScene;

        [Tooltip("移動先シーン名（InterScene時のみ使用）")]
        [SerializeField] private string destinationScene = "";

        [Tooltip("移動先の SpawnPoint ID")]
        [SerializeField] private string spawnPointId = "Default";

        [Tooltip("同一シーン移動時、直接ターゲットTransformを指定する場合（任意）")]
        [SerializeField] private Transform targetTransform;

        [Header("Activation & Input")]
        [SerializeField] private DoorActivationMode activationMode = DoorActivationMode.InteractOnly;
        [SerializeField] private string requiredKey = "Interact";
        [SerializeField] private string promptActionDescription = "入る";

        [Header("Transition Style")]
        [SerializeField] private TransitionStyle transitionStyle = TransitionStyle.FadeOnly;
        [SerializeField] private float fadeDuration = 0.4f;

        [Header("Locked Condition (FlagManager)")]
        [Tooltip("移動に必要なフラグ条件（空の場合は常に開いている）")]
        [SerializeField] private string requiredFlagKey = "";
        [SerializeField] private int requiredFlagValue = 1;

        [Tooltip("鍵がかかっている場合に表示するメッセージ")]
        [SerializeField] private string lockedMessage = "鍵がかかっているようだ…";

        private bool isLocked = false;

        private void Start()
        {
            RefreshLockState();
        }

        private void RefreshLockState()
        {
            if (!string.IsNullOrEmpty(requiredFlagKey) && FlagManager.Instance != null)
            {
                isLocked = FlagManager.Instance.GetFlag(requiredFlagKey) < requiredFlagValue;
            }
            else
            {
                isLocked = false;
            }
        }

        // ==========================================
        // 🚪 移動トリガー実行
        // ==========================================

        public void TriggerTransition()
        {
            RefreshLockState();

            if (isLocked)
            {
                Debug.Log($"[AreaDoor2D] '{gameObject.name}' は施錠されています: {lockedMessage}");
                // 必要ならここでSE再生やUI通知
                return;
            }

            var request = new TransitionRequest
            {
                destinationScene = triggerType == DoorTriggerType.InterScene ? destinationScene : "",
                spawnPointId     = spawnPointId,
                targetTransform  = targetTransform,
                style            = transitionStyle,
                fadeDuration     = fadeDuration
            };

            AreaTransitionManager.Instance.Transition(request);
        }

        // ==========================================
        // 🤝 IInteractable2D 実装
        // ==========================================

        public string RequiredKey => requiredKey;

        public bool TryInteract(string key)
        {
            if (activationMode == DoorActivationMode.TouchOnly) return false;
            if (key != requiredKey) return false;

            TriggerTransition();
            return true;
        }

        public string GetInteractPrompt()
        {
            if (activationMode == DoorActivationMode.TouchOnly) return string.Empty;

            RefreshLockState();
            if (isLocked)
            {
                return InputPromptResolver.GetPromptText(requiredKey, "調べる（施錠）");
            }
            return InputPromptResolver.GetPromptText(requiredKey, promptActionDescription);
        }

        // ==========================================
        // 🚶 接触トリガー（マップ端・階段用）
        // ==========================================

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (activationMode == DoorActivationMode.InteractOnly) return;

            // プレイヤーとの接触か判定
            if (other.CompareTag("Player") || other.GetComponent<Character.PlayerController2D>() != null)
            {
                TriggerTransition();
            }
        }
    }
}
