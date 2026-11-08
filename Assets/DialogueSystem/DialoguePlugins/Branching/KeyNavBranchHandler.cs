using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;
using Runtime.Dialogue.Audio;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Runtime.Dialogue.Branching
{
    [HandlerInfo(
        description: "キーボードの上下（またはW/S・コントローラー）でカーソルを移動させ、決定キーで選択するキーナビゲーション型の分岐ハンドラーです。",
        usage: "1. ノードエディタ上で選択肢の BranchType に「KeyNavBranchHandler」を指定します。\n" +
               "2. Inspector の『Branch UI Prefab』に独自デザインのプレハブを割り当ててください。\n" +
               "3. カーソル画像や選択時ハイライトスプライト・SEも自由にカスタマイズ可能です。"
    )]
    public class KeyNavBranchHandler : MonoBehaviour, IDialogueBranchHandler
    {
        public int Priority => 60; // PrefabUIBranchHandler (50) より優先

        [Header("UI Prefab Settings")]
        [SerializeField, Tooltip("選択肢を表示するUIプレハブ（CanvasまたはPanel）")]
        private GameObject branchUIPrefab;

        [SerializeField, Tooltip("プレハブ生成先の親Transform（未指定時は自身またはルートCanvas）")]
        private Transform uiParent;

        [Header("Cursor & Selection Visuals")]
        [SerializeField, Tooltip("選択中の項目の横に表示するカーソルアイコン（任意）")]
        private GameObject cursorObject;

        [SerializeField, Tooltip("カーソルの項目に対する相対オフセット位置")]
        private Vector2 cursorOffset = new Vector2(-40f, 0f);

        [SerializeField, Tooltip("選択中のボタンスプライト（任意）")]
        private Sprite selectedSprite;

        [SerializeField, Tooltip("非選択時のボタンスプライト（任意）")]
        private Sprite normalSprite;

        [SerializeField, Tooltip("選択中のテキストカラー")]
        private Color selectedTextColor = Color.yellow;

        [SerializeField, Tooltip("非選択時のテキストカラー")]
        private Color normalTextColor = Color.white;

        [SerializeField, Tooltip("選択中のボタンの拡大率")]
        private float selectedScale = 1.05f;

        [Header("Audio Settings")]
        [SerializeField, Tooltip("上下カーソル移動時のSEキー"), DialogueAudioKey(Runtime.Dialogue.Audio.AudioType.SE)]
        private string cursorSEKey = "cursor_move";

        [SerializeField, Tooltip("決定時のSEキー"), DialogueAudioKey(Runtime.Dialogue.Audio.AudioType.SE)]
        private string submitSEKey = "choice_select";

        [Header("Navigation Settings")]
        [SerializeField, Tooltip("端まで行った時にループするかどうか")]
        private bool loopNavigation = true;

        [SerializeField, Tooltip("長押し時の連続スクロール遅延（秒）")]
        private float repeatDelay = 0.2f;

        // ランタイム管理用
        private GameObject currentUIInstance;
        private List<Button> activeButtons = new List<Button>();
        private List<ChoiceData> currentChoices = new List<ChoiceData>();
        private int currentIndex = 0;
        private Action<string> currentCallback;
        private Coroutine inputCoroutine;
        private GameObject spawnedCursor;

        private void Start()
        {
            if (DialogueBranchDispatcher.Instance != null)
            {
                DialogueBranchDispatcher.Instance.RegisterHandler(this);
            }
        }

        public bool TryHandleBranch(List<ChoiceData> choices, Action<string> onBranchDecided)
        {
            if (choices == null || choices.Count == 0) return false;

            string targetBranchType = this.GetType().Name;

            // 1. branchType とフラグ条件でフィルタリング
            var validChoices = choices.Where(c =>
                c.branchType == targetBranchType &&
                (FlagManager.Instance == null || FlagManager.Instance.EvaluateCondition(c.conditionKey, c.conditionOperator, c.conditionValue))
            ).ToList();

            if (validChoices.Count == 0) return false;

            if (branchUIPrefab == null)
            {
                Debug.LogError($"[{targetBranchType}] Branch UI Prefab が設定されていません。Inspector から割り当ててください。");
                return false;
            }

            // 2. UIプレハブを生成
            currentUIInstance = uiParent != null ? Instantiate(branchUIPrefab, uiParent) : Instantiate(branchUIPrefab);
            currentChoices = validChoices;
            currentCallback = onBranchDecided;

            // プレハブ内の Button を全取得
            Button[] allButtons = currentUIInstance.GetComponentsInChildren<Button>(true);
            activeButtons.Clear();

            int processCount = Mathf.Min(validChoices.Count, allButtons.Length);
            if (processCount == 0)
            {
                Destroy(currentUIInstance);
                return false;
            }

            for (int i = 0; i < allButtons.Length; i++)
            {
                Button btn = allButtons[i];
                if (i < processCount)
                {
                    btn.gameObject.SetActive(true);
                    activeButtons.Add(btn);

                    int index = i;
                    var choice = validChoices[i];

                    // テキスト設定 (TextMeshPro または 通常Text)
                    var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = choice.choiceText;
                    else
                    {
                        var txt = btn.GetComponentInChildren<Text>();
                        if (txt != null) txt.text = choice.choiceText;
                    }

                    // クリック時リスナー
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => SelectChoice(index));
                }
                else
                {
                    btn.gameObject.SetActive(false);
                }
            }

            // 3. カーソルの準備
            if (cursorObject != null)
            {
                spawnedCursor = Instantiate(cursorObject, currentUIInstance.transform);
                spawnedCursor.SetActive(true);
            }

            // 4. 初期選択
            currentIndex = 0;
            UpdateSelectionVisuals(playSE: false);

            // 5. キー入力監視コルーチン開始
            if (inputCoroutine != null) StopCoroutine(inputCoroutine);
            inputCoroutine = StartCoroutine(InputDetectionRoutine());

            return true;
        }

        private IEnumerator InputDetectionRoutine()
        {
            float inputTimer = 0f;

            while (currentUIInstance != null && activeButtons.Count > 0)
            {
                float vertical = GetVerticalInput();
                bool submit = GetSubmitInput();

                if (Mathf.Abs(vertical) > 0.5f)
                {
                    if (inputTimer <= 0f)
                    {
                        if (vertical > 0.5f)
                        {
                            Navigate(-1); // 上
                        }
                        else if (vertical < -0.5f)
                        {
                            Navigate(1);  // 下
                        }
                        inputTimer = repeatDelay;
                    }
                    else
                    {
                        inputTimer -= Time.unscaledDeltaTime;
                    }
                }
                else
                {
                    inputTimer = 0f;
                }

                if (submit)
                {
                    SelectChoice(currentIndex);
                    yield break;
                }

                yield return null;
            }
        }

        private void Navigate(int delta)
        {
            if (activeButtons.Count <= 1) return;

            int prev = currentIndex;
            currentIndex += delta;

            if (loopNavigation)
            {
                if (currentIndex < 0) currentIndex = activeButtons.Count - 1;
                else if (currentIndex >= activeButtons.Count) currentIndex = 0;
            }
            else
            {
                currentIndex = Mathf.Clamp(currentIndex, 0, activeButtons.Count - 1);
            }

            if (prev != currentIndex)
            {
                UpdateSelectionVisuals(playSE: true);
            }
        }

        private void UpdateSelectionVisuals(bool playSE)
        {
            if (playSE && !string.IsNullOrEmpty(cursorSEKey))
            {
                DialogueAudioManager.Instance?.PlaySE(cursorSEKey);
            }

            for (int i = 0; i < activeButtons.Count; i++)
            {
                Button btn = activeButtons[i];
                bool isSelected = (i == currentIndex);

                // スプライト切り替え
                var img = btn.GetComponent<Image>();
                if (img != null)
                {
                    if (isSelected && selectedSprite != null) img.sprite = selectedSprite;
                    else if (!isSelected && normalSprite != null) img.sprite = normalSprite;
                }

                // テキストカラー
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.color = isSelected ? selectedTextColor : normalTextColor;
                }
                else
                {
                    var txt = btn.GetComponentInChildren<Text>();
                    if (txt != null) txt.color = isSelected ? selectedTextColor : normalTextColor;
                }

                // スケール
                btn.transform.localScale = isSelected ? Vector3.one * selectedScale : Vector3.one;

                // カーソル位置の更新
                if (isSelected && spawnedCursor != null)
                {
                    spawnedCursor.transform.position = btn.transform.position + (Vector3)cursorOffset;
                }
            }
        }

        private void SelectChoice(int index)
        {
            if (index < 0 || index >= currentChoices.Count) return;

            if (!string.IsNullOrEmpty(submitSEKey))
            {
                DialogueAudioManager.Instance?.PlaySE(submitSEKey);
            }

            string nextNodeID = currentChoices[index].targetNodeID;

            Cleanup();
            currentCallback?.Invoke(nextNodeID);
        }

        private void Cleanup()
        {
            if (inputCoroutine != null)
            {
                StopCoroutine(inputCoroutine);
                inputCoroutine = null;
            }

            if (currentUIInstance != null)
            {
                Destroy(currentUIInstance);
                currentUIInstance = null;
            }

            activeButtons.Clear();
            currentChoices.Clear();
            spawnedCursor = null;
        }

        // --- 入力検知（新旧Input両対応） ---
        private float GetVerticalInput()
        {
#if ENABLE_INPUT_SYSTEM
            float v = 0f;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.isPressed || kb.wKey.isPressed) v += 1f;
                if (kb.downArrowKey.isPressed || kb.sKey.isPressed) v -= 1f;
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.dpad.up.isPressed || gp.leftStick.up.isPressed) v += 1f;
                if (gp.dpad.down.isPressed || gp.leftStick.down.isPressed) v -= 1f;
            }

            if (Mathf.Abs(v) > 0.1f) return v;
#endif
            // 旧Inputフォールバック
            return Input.GetAxisRaw("Vertical");
        }

        private bool GetSubmitInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame)
                {
                    return true;
                }
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.buttonSouth.wasPressedThisFrame) return true; // Aボタン / ✕ボタン
            }
#endif
            // 旧Inputフォールバック
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetButtonDown("Submit");
        }
    }
}
