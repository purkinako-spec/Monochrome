using System;
using UnityEngine;
using Runtime.Dialogue;
using Runtime.Dialogue.Core;

namespace Monochrome.Core.Character
{
    /// <summary>
    /// 会話ノード内からキャラクターの自動移動を指示するコマンドハンドラー。
    /// 書式: [move:target=Player, to=MonumentPoint, style=Run, wait=true]
    /// </summary>
    [AddComponentMenu("Monochrome/Cutscene/Move Command Handler")]
    public class CutsceneMoveCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "move";

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }
        }

        public async void Execute(DialogueCommand command, Action onComplete)
        {
            string targetName = command.GetString("target", "Player");
            string destinationName = command.GetString("to", "");
            string styleStr = command.GetString("style", "Walk");
            bool wait = command.GetBool("wait", true);

            var character = FindCharacter(targetName);
            if (character == null)
            {
                Debug.LogWarning($"[CutsceneMove] 対象キャラクター '{targetName}' が見つかりません。");
                onComplete?.Invoke();
                return;
            }

            var nav = character.GetComponent<CharacterNavigator2D>();
            if (nav == null)
            {
                nav = character.AddComponent<CharacterNavigator2D>();
            }

            Vector2 destination = ResolveDestination(destinationName);
            Enum.TryParse<MovementStyle>(styleStr, true, out var style);

            if (!wait)
            {
                // 待機しない場合は歩行指示を出して即座にセリフ進行へ
                _ = nav.MoveToAsync(destination, style);
                onComplete?.Invoke();
            }
            else
            {
                // 到着までセリフ進行を一時停止
                await nav.MoveToAsync(destination, style);
                onComplete?.Invoke();
            }
        }

        public void ForceComplete(DialogueCommand command)
        {
            string targetName = command.GetString("target", "Player");
            string destinationName = command.GetString("to", "");

            var character = FindCharacter(targetName);
            if (character != null)
            {
                var motor = character.GetComponent<CharacterMotor2D>();
                if (motor != null)
                {
                    Vector2 dest = ResolveDestination(destinationName);
                    motor.WarpTo(dest);
                }
            }
        }

        private GameObject FindCharacter(string name)
        {
            if (name.Equals("Player", StringComparison.OrdinalIgnoreCase))
            {
                if (PlayerController2D.LocalPlayer != null)
                    return PlayerController2D.LocalPlayer.gameObject;
                return GameObject.FindGameObjectWithTag("Player");
            }
            return GameObject.Find(name);
        }

        private Vector2 ResolveDestination(string destinationName)
        {
            var targetObj = GameObject.Find(destinationName);
            if (targetObj != null) return targetObj.transform.position;

            // X,Y 座標の直接指定をパース (例: "5.0,2.5")
            if (destinationName.Contains(","))
            {
                var parts = destinationName.Split(',');
                if (parts.Length >= 2 &&
                    float.TryParse(parts[0], out float x) &&
                    float.TryParse(parts[1], out float y))
                {
                    return new Vector2(x, y);
                }
            }
            return Vector2.zero;
        }
    }

    /// <summary>
    /// キャラクターの向き・視線変更を行うコマンドハンドラー。
    /// 書式: [lookat:target=Elder, towards=Player] または [lookat:target=Player, towards=Up]
    /// </summary>
    [AddComponentMenu("Monochrome/Cutscene/LookAt Command Handler")]
    public class CutsceneLookAtCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "lookat";

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            string targetName = command.GetString("target", "Player");
            string towards = command.GetString("towards", "");

            var targetObj = FindTarget(targetName);
            if (targetObj != null)
            {
                var motor = targetObj.GetComponent<CharacterMotor2D>();
                if (motor != null)
                {
                    Vector2 dir = ResolveDirection(targetObj.transform.position, towards);
                    motor.SetFacingDirection(dir);
                }
            }

            onComplete?.Invoke();
        }

        public void ForceComplete(DialogueCommand command) => Execute(command, null);

        private GameObject FindTarget(string name)
        {
            if (name.Equals("Player", StringComparison.OrdinalIgnoreCase))
            {
                if (PlayerController2D.LocalPlayer != null)
                    return PlayerController2D.LocalPlayer.gameObject;
                return GameObject.FindGameObjectWithTag("Player");
            }
            return GameObject.Find(name);
        }

        private Vector2 ResolveDirection(Vector3 origin, string towards)
        {
            switch (towards.ToLower())
            {
                case "up": return Vector2.up;
                case "down": return Vector2.down;
                case "left": return Vector2.left;
                case "right": return Vector2.right;
            }

            var destObj = FindTarget(towards);
            if (destObj != null)
            {
                return destObj.transform.position - origin;
            }
            return Vector2.down;
        }
    }

    /// <summary>
    /// 感情ジェスチャーやアニメーショントリガーを再生するコマンドハンドラー。
    /// 書式: [anim:target=Player, trigger=Surprise]
    /// </summary>
    [AddComponentMenu("Monochrome/Cutscene/Anim Command Handler")]
    public class CutsceneAnimCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "anim";

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            string targetName = command.GetString("target", "Player");
            string triggerName = command.GetString("trigger", "");

            var targetObj = GameObject.Find(targetName);
            if (targetName.Equals("Player", StringComparison.OrdinalIgnoreCase) && PlayerController2D.LocalPlayer != null)
            {
                targetObj = PlayerController2D.LocalPlayer.gameObject;
            }

            if (targetObj != null)
            {
                var visual = targetObj.GetComponent<CharacterVisual2D>();
                if (visual != null)
                {
                    if (Enum.TryParse<EmoteType>(triggerName, true, out var emote))
                    {
                        visual.PlayEmote(emote);
                    }
                    else
                    {
                        visual.PlayCustomTrigger(triggerName);
                    }
                }
            }

            onComplete?.Invoke();
        }

        public void ForceComplete(DialogueCommand command) => Execute(command, null);
    }

    /// <summary>
    /// 頭上に！や？の感情バルーンを表示するコマンドハンドラー。
    /// 書式: [balloon:target=Player, type=Exclamation, duration=1.2]
    /// </summary>
    [AddComponentMenu("Monochrome/Cutscene/Balloon Command Handler")]
    public class CutsceneBalloonCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "balloon";

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            string targetName = command.GetString("target", "Player");
            string typeStr = command.GetString("type", "Exclamation");
            float duration = command.GetFloat("duration", 1.2f);

            var targetObj = GameObject.Find(targetName);
            if (targetName.Equals("Player", StringComparison.OrdinalIgnoreCase) && PlayerController2D.LocalPlayer != null)
            {
                targetObj = PlayerController2D.LocalPlayer.gameObject;
            }

            if (targetObj != null)
            {
                var balloon = targetObj.GetComponent<EmotionBalloonController>();
                if (balloon == null)
                {
                    balloon = targetObj.AddComponent<EmotionBalloonController>();
                }

                if (Enum.TryParse<BalloonType>(typeStr, true, out var balloonType))
                {
                    balloon.ShowBalloon(balloonType, duration);
                }
            }

            onComplete?.Invoke();
        }

        public void ForceComplete(DialogueCommand command) => Execute(command, null);
    }
}
