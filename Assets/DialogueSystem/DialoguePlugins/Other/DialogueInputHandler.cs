using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Runtime.Dialogue.Audio;
using Runtime.Dialogue.Logic;

namespace Runtime.Dialogue
{
    /// <summary>
    /// 会話進行・オート・スキップのユーザー入力と効果音（SE）を統括するハンドラー
    /// (New Input System に対応)
    /// </summary>
    public class DialogueInputHandler : MonoBehaviour
    {
        [Header("SE Keys")]
        [SerializeField] private string nextPageSEKey = "next_page";

        [Header("References")]
        [SerializeField] private DialogueViewWindow dialogueView;

        [Header("Input Actions (任意・リバインド対応)")]
        [Tooltip("会話送りアクション（未設定時はSpace/Enter/Fキー/マウスクリック/ゲームパッド南ボタンで動作）")]
        [SerializeField] private InputActionReference advanceAction;

        [Tooltip("オートモード切替アクション（未設定時はAキー/ゲームパッド西ボタンで動作）")]
        [SerializeField] private InputActionReference autoModeAction;

        [Tooltip("スキップモード切替アクション（未設定時はSキー/ゲームパッド北ボタンで動作）")]
        [SerializeField] private InputActionReference skipModeAction;

        private float startInputCooldown = 0f;
        private DialogueState prevState = DialogueState.Idle;

        private DialogueViewWindow ActiveView => (DialogueManager.Instance?.CurrentView as DialogueViewWindow) ?? dialogueView;

        private void OnEnable()
        {
            if (advanceAction != null && advanceAction.action != null) advanceAction.action.Enable();
            if (autoModeAction != null && autoModeAction.action != null) autoModeAction.action.Enable();
            if (skipModeAction != null && skipModeAction.action != null) skipModeAction.action.Enable();
        }

        private void OnDisable()
        {
            if (advanceAction != null && advanceAction.action != null) advanceAction.action.Disable();
            if (autoModeAction != null && autoModeAction.action != null) autoModeAction.action.Disable();
            if (skipModeAction != null && skipModeAction.action != null) skipModeAction.action.Disable();
        }

        private void Awake()
        {
            if (dialogueView == null)
            {
                dialogueView = UnityEngine.Object.FindAnyObjectByType<DialogueViewWindow>();
            }
        }

        private void Update()
        {
            if (DialogueManager.Instance == null) return;

            DialogueState currentState = DialogueManager.Instance.CurrentState;

            // 会話開始直後（Idle -> 会話中への遷移時）にクールダウンを設けて連打・押しっぱなしによる誤送りを防止
            if (prevState == DialogueState.Idle && currentState != DialogueState.Idle)
            {
                startInputCooldown = 0.2f;
            }
            prevState = currentState;

            if (currentState == DialogueState.Idle) return;

            if (startInputCooldown > 0f)
            {
                startInputCooldown -= Time.deltaTime;
                return;
            }

            // --- 入力判定 (New Input System / Gamepad / Keyboard / Mouse) ---
            bool isActionTriggered = advanceAction != null && advanceAction.action != null && advanceAction.action.WasPressedThisFrame();

            // 1. マウスクリック
            bool isMousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

            // 2. キーボード（Space, Enter, テンキーEnter, Fキー）
            bool isStandardKeyboard = Keyboard.current != null && (
                Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                Keyboard.current.fKey.wasPressedThisFrame
            );

            // 3. ゲームパッド（南ボタン: A / ✕）
            bool isGamepadPressed = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;

            if (isActionTriggered || isMousePressed || isStandardKeyboard || isGamepadPressed)
            {
                // 選択肢が表示されている間は、UIへのクリック誤爆を防ぐためガード
                if (isMousePressed && ActiveView != null && ActiveView.IsShowingChoices)
                {
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(PointerInputModule.kMouseLeftId))
                    {
                        return;
                    }
                }

                HandleAdvance();
            }

            // 4. オートモード切り替え（Action または Aキー / ゲームパッド西ボタン）
            bool isAutoTriggered = (autoModeAction != null && autoModeAction.action != null && autoModeAction.action.WasPressedThisFrame())
                || (Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);

            if (isAutoTriggered)
            {
                DialogueManager.Instance.ToggleAutoMode();
            }

            // 5. スキップモード切り替え（Action または Sキー / ゲームパッド北ボタン）
            bool isSkipTriggered = (skipModeAction != null && skipModeAction.action != null && skipModeAction.action.WasPressedThisFrame())
                || (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);

            if (isSkipTriggered)
            {
                DialogueManager.Instance.ToggleSkipMode();
            }
        }

        /// <summary>
        /// 会話を次に進める（効果音を鳴らしManagerを呼び出し）
        /// </summary>
        public void HandleAdvance()
        {
            if (ActiveView != null && ActiveView.IsShowingChoices) return;

            // 会話送りSE再生
            DialogueAudioManager.Instance?.PlaySE(nextPageSEKey);

            // DialogueManagerへ進行通知
            DialogueManager.Instance?.HandleAdvanceInput();
        }
    }
}