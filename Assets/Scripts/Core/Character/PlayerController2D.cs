using UnityEngine;
using UnityEngine.InputSystem;
using Monochrome.Core.Input;

namespace Monochrome.Core.Character
{
    /// <summary>
    /// 新しいプレイヤーコントローラー。
    /// - Input System の Action から入力を受け取り、CharacterMotor2D へ伝達
    /// - ActionBlockState を保持し、会話中・エリア移動中・ミニゲーム中の多重操作ロックを安全に制御
    /// - LocalPlayer 静的インスタンスを提供し、他システムから一意にアクセス可能
    /// </summary>
    [RequireComponent(typeof(CharacterMotor2D))]
    [AddComponentMenu("Monochrome/Player/Player Controller 2D")]
    public class PlayerController2D : MonoBehaviour
    {
        public static PlayerController2D LocalPlayer { get; private set; }

        [Header("Input Action References")]
        [Tooltip("移動入力アクション (Vector2, 例: Player/Move)")]
        [SerializeField] private InputActionReference moveAction;

        [Tooltip("ダッシュ入力アクション (Button, 例: Player/Sprint)")]
        [SerializeField] private InputActionReference sprintAction;

        // コンポーネント参照
        private CharacterMotor2D motor;
        private readonly ActionBlockState blockState = new ActionBlockState();

        public CharacterMotor2D Motor => motor;
        public ActionBlockState BlockState => blockState;

        private void Awake()
        {
            if (LocalPlayer == null || !LocalPlayer)
            {
                LocalPlayer = this;
            }
            motor = GetComponent<CharacterMotor2D>();

            // インスペクタ未割り当て時の自動フォールバック探索
            if (moveAction == null || sprintAction == null)
            {
                AutoBindInputActions();
            }
        }

        private void AutoBindInputActions()
        {
            var assets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
            foreach (var asset in assets)
            {
                if (asset.name.Contains("InputSystem"))
                {
                    var map = asset.FindActionMap("Player");
                    if (map != null)
                    {
                        if (moveAction == null)
                        {
                            var act = map.FindAction("Move");
                            if (act != null) moveAction = InputActionReference.Create(act);
                        }
                        if (sprintAction == null)
                        {
                            var act = map.FindAction("Sprint");
                            if (act != null) sprintAction = InputActionReference.Create(act);
                        }
                        break;
                    }
                }
            }
        }

        private void OnEnable()
        {
            EnableActions();
            blockState.OnBlockStateChanged += HandleBlockStateChanged;
        }

        private void OnDisable()
        {
            DisableActions();
            blockState.OnBlockStateChanged -= HandleBlockStateChanged;
        }

        private void OnDestroy()
        {
            if (LocalPlayer == this)
            {
                LocalPlayer = null;
            }
        }

        private void Update()
        {
            ProcessInput();
        }

        private void ProcessInput()
        {
            if (motor == null) return;

            // 操作がブロックされている場合は移動を遮断
            if (!blockState.CanMove)
            {
                motor.SetMovementInput(Vector2.zero);
                return;
            }

            // 移動ベクトル取得
            Vector2 inputVector = Vector2.zero;
            if (moveAction != null && moveAction.action != null)
            {
                inputVector = moveAction.action.ReadValue<Vector2>();
            }

            // スプリント（ダッシュ）判定
            bool isSprinting = false;
            if (sprintAction != null && sprintAction.action != null)
            {
                isSprinting = sprintAction.action.IsPressed();
            }

            motor.SetMovementStyle(isSprinting ? MovementStyle.Run : MovementStyle.Walk);
            motor.SetMovementInput(inputVector);
        }

        private void HandleBlockStateChanged(ActionBlocker currentBlockers)
        {
            if (!blockState.CanMove && motor != null)
            {
                motor.StopImmediately();
            }
        }

        private void EnableActions()
        {
            if (moveAction != null && moveAction.action != null) moveAction.action.Enable();
            if (sprintAction != null && sprintAction.action != null) sprintAction.action.Enable();
        }

        private void DisableActions()
        {
            if (moveAction != null && moveAction.action != null) moveAction.action.Disable();
            if (sprintAction != null && sprintAction.action != null) sprintAction.action.Disable();
        }
    }
}
