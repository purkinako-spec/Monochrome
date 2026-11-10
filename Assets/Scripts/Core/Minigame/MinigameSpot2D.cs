using UnityEngine;
using InteractionSystem2D;
using Monochrome.Core.Input;
using Runtime.Dialogue.Logic;

namespace Monochrome.Core.Minigame
{
    /// <summary>
    /// フィールド上に配置するミニゲーム突入スポット（釣り場など）。
    /// - 既存の IInteractable2D を実装し、プレイヤーの [E] / (A) 入力でミニゲームを起動
    /// - FlagManager と直結したアイテム所持・進行条件判定（未達成時はロック表示）
    /// - 終了後のクリアフラグ更新や結果会話の指定
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Monochrome/Minigame/Minigame Spot 2D")]
    public class MinigameSpot2D : MonoBehaviour, IInteractable2D
    {
        [Header("Minigame Settings")]
        [Tooltip("加算（Additive）ロードするミニゲームシーン名")]
        [SerializeField] private string minigameSceneName = "FishingMinigameSceneTest1";

        [Tooltip("ミニゲームクリア時に立てるフラグ（例: 'quest_fish_cleared'）")]
        [SerializeField] private string clearFlagKey = "minigame_fishing_cleared";

        [Tooltip("ミニゲーム終了後に自動で再生する会話コンテナ名（任意）")]
        [SerializeField] private string resultDialogueContainer = "";

        [Header("Interaction & Input")]
        [SerializeField] private string requiredKey = "Interact";
        [SerializeField] private string spotActionDescription = "釣りをする";

        [Header("Requirement Condition (FlagManager)")]
        [Tooltip("プレイに必要な条件フラグ（例: 'has_fishing_rod'。空なら無条件）")]
        [SerializeField] private string requiredFlagKey = "";
        [SerializeField] private int requiredFlagValue = 1;

        [Tooltip("条件を満たしていない場合のプロンプト説明")]
        [SerializeField] private string missingRequirementDescription = "調べる（釣竿が必要）";

        public string RequiredKey => requiredKey;

        public bool TryInteract(string key)
        {
            if (key != requiredKey) return false;

            if (!IsRequirementMet())
            {
                Debug.Log($"[MinigameSpot2D] 条件を満たしていません: {requiredFlagKey} が必要です。");
                return false;
            }

            var request = new MinigameLaunchRequest
            {
                minigameSceneName       = minigameSceneName,
                clearFlagKey            = clearFlagKey,
                resultDialogueContainer = resultDialogueContainer,
                useFade                 = true
            };

            MinigameConnector.Instance.LaunchMinigame(request);
            return true;
        }

        public string GetInteractPrompt()
        {
            if (!IsRequirementMet())
            {
                return InputPromptResolver.GetPromptText(requiredKey, missingRequirementDescription);
            }
            return InputPromptResolver.GetPromptText(requiredKey, spotActionDescription);
        }

        private bool IsRequirementMet()
        {
            if (string.IsNullOrEmpty(requiredFlagKey) || FlagManager.Instance == null)
            {
                return true;
            }
            return FlagManager.Instance.GetFlag(requiredFlagKey) >= requiredFlagValue;
        }
    }
}
