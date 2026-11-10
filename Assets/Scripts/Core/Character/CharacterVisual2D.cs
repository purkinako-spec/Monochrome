using System;
using UnityEngine;

namespace Monochrome.Core.Character
{
    public enum EmoteType
    {
        Surprise,   // 驚く（跳ねる）
        Nod,        // うなずく
        ShakeHead,  // 首を振る
        LookAround, // 見回す
        Sit,        // 座る
        Stand,      // 立つ
        Collapse    // 倒れる
    }

    /// <summary>
    /// キャラクターの感情ジェスチャー・エモートアニメーション・視線（LookAt）を制御するコンポーネント。
    /// 会話やイベント演出中にアニメーショントリガーを安全に発火します。
    /// </summary>
    [AddComponentMenu("Monochrome/Character/Character Visual 2D")]
    public class CharacterVisual2D : MonoBehaviour
    {
        [Header("Animator Reference")]
        [SerializeField] private Animator animator;

        [Header("Motor Reference")]
        [SerializeField] private CharacterMotor2D motor;

        // トリガーハッシュのキャッシュ
        private static readonly int HashSurprise   = Animator.StringToHash("Surprise");
        private static readonly int HashNod        = Animator.StringToHash("Nod");
        private static readonly int HashShakeHead  = Animator.StringToHash("ShakeHead");
        private static readonly int HashLookAround = Animator.StringToHash("LookAround");
        private static readonly int HashSit        = Animator.StringToHash("Sit");
        private static readonly int HashStand      = Animator.StringToHash("Stand");
        private static readonly int HashCollapse   = Animator.StringToHash("Collapse");

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (motor == null) motor = GetComponent<CharacterMotor2D>();
        }

        /// <summary>
        /// 指定した感情ジェスチャーアニメーションを再生します
        /// </summary>
        public void PlayEmote(EmoteType emote)
        {
            if (animator == null) return;

            int triggerHash = emote switch
            {
                EmoteType.Surprise   => HashSurprise,
                EmoteType.Nod        => HashNod,
                EmoteType.ShakeHead  => HashShakeHead,
                EmoteType.LookAround => HashLookAround,
                EmoteType.Sit        => HashSit,
                EmoteType.Stand      => HashStand,
                EmoteType.Collapse   => HashCollapse,
                _                    => 0
            };

            if (triggerHash != 0)
            {
                animator.SetTrigger(triggerHash);
            }
        }

        /// <summary>
        /// 任意のアニメーショントリガー名で再生します
        /// </summary>
        public void PlayCustomTrigger(string triggerName)
        {
            if (animator != null && !string.IsNullOrEmpty(triggerName))
            {
                animator.SetTrigger(triggerName);
            }
        }

        /// <summary>
        /// 指定した座標の方向を向きます
        /// </summary>
        public void LookAt(Vector2 targetPosition)
        {
            if (motor != null)
            {
                Vector2 dir = targetPosition - (Vector2)transform.position;
                motor.SetFacingDirection(dir);
            }
        }

        /// <summary>
        /// 指定した Transform の方向を向きます
        /// </summary>
        public void LookAt(Transform target)
        {
            if (target != null)
            {
                LookAt((Vector2)target.position);
            }
        }
    }
}
