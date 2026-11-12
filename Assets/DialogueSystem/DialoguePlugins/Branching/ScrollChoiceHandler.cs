using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
        description: "登録したUIパネルPrefab内のボタン数を表示枠として、選択肢を1,2,3→2,3,4のようにウィンドウ送りで切り替えるキーナビゲーション型ブランチハンドラーです。上下端でのループやカーソル追従に対応します。",
        usage: "1. ノードエディタ上で選択肢の BranchType に「ScrollChoiceHandler」または「ScrollChoice」を指定します。\n" +
               "2. Inspector の『Branch UI Prefab』にボタンを配置したパネルPrefab（例: UIButtonPanel, 2PatternButtonPanel）を登録します。\n" +
               "3. カーソル画像やSEも自由にカスタマイズ可能です。"
    )]
    [AddComponentMenu("Dialogue/Branching/Scroll Choice Handler")]
    public class ScrollChoiceHandler : MonoBehaviour, IDialogueBranchHandler
    {
        public int Priority => 55; // DefaultChoiceHandler (0) より優先

        [Header("UI Prefab Settings")]
        [SerializeField, Tooltip("選択肢を表示するUIパネルPrefab（UIButtonPanel, 2PatternButtonPanel等）")]
        private GameObject branchUIPrefab;

        [SerializeField, Tooltip("生成先親Transform（未指定時はシーン上のCanvas等を自動検索）")]
        private Transform uiParent;

        [Header("Cursor Settings")]
        [SerializeField, Tooltip("選択中ボタンの横に表示するカーソルオブジェクト（未指定時はPrefab内を検索または自動生成）")]
        private GameObject cursorObject;

        [SerializeField, Tooltip("カーソルの選択中ボタンに対する相対オフセット位置")]
        private Vector2 cursorOffset = new Vector2(-40f, 0f);

        [Header("Button Visuals (任意)")]
        [SerializeField, Tooltip("選択中のボタンスプライト（任意）")]
        private Sprite selectedSprite;

        [SerializeField, Tooltip("通常時のボタンスプライト（任意）")]
        private Sprite normalSprite;

        [SerializeField, Tooltip("選択中のテキストカラー")]
        private Color selectedTextColor = Color.yellow;

        [SerializeField, Tooltip("通常時のテキストカラー")]
        private Color normalTextColor = Color.white;

        [Header("Audio Settings")]
        [SerializeField, Tooltip("上下カーソル移動時のSEキー"), DialogueAudioKey(Runtime.Dialogue.Audio.AudioType.SE)]
        private string cursorSEKey = "cursor_move";

        [SerializeField, Tooltip("決定時のSEキー"), DialogueAudioKey(Runtime.Dialogue.Audio.AudioType.SE)]
        private string submitSEKey = "choice_select";

        [Header("Navigation Settings")]
        [SerializeField, Tooltip("端まで行った時にループするかどうか（1から上で一番下、一番下から下で1へ）")]
        private bool loopNavigation = true;

        [SerializeField, Tooltip("長押し時の連続移動ディレイ（秒）")]
        private float repeatDelay = 0.2f;

        // ランタイム管理用
        private GameObject currentUIInstance;
        private List<Button> slotButtons = new List<Button>();
        private List<ChoiceData> validChoices = new List<ChoiceData>();
        private int currentIndex = 0;   // 全選択肢中の選択インデックス (0 .. N-1)
        private int windowStart = 0;    // スロット先頭に表示している選択肢インデックス (0 .. N-M)
        private Action<string> currentCallback;
        private Coroutine inputCoroutine;
        private GameObject spawnedCursor;

        private void Awake() => TryRegister();
        private void OnEnable() => TryRegister();
        private void Start() => TryRegister();

        private void TryRegister()
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
            validChoices = choices.Where(c =>
                (c.branchType == targetBranchType || c.branchType == "ScrollChoice") &&
                (FlagManager.Instance == null || FlagManager.Instance.EvaluateCondition(c.conditionKey, c.conditionOperator, c.conditionValue))
            ).ToList();

            if (validChoices.Count == 0) return false;

            if (branchUIPrefab == null)
            {
                Debug.LogError($"[{targetBranchType}] Branch UI Prefab が設定されていません。Inspector から割り当ててください。");
                return false;
            }

            // 2. UIパネルを生成
            Transform parent = uiParent != null ? uiParent : FindRootCanvas();
            currentUIInstance = parent != null ? Instantiate(branchUIPrefab, parent) : Instantiate(branchUIPrefab);

            // 3. パネル内のボタンを収集し、画面の上から下（Y座標降順）にソート
            slotButtons = currentUIInstance.GetComponentsInChildren<Button>(true)
                .OrderByDescending(b => b.transform.position.y)
                .ToList();

            if (slotButtons.Count == 0)
            {
                Debug.LogError($"[{targetBranchType}] Prefab '{branchUIPrefab.name}' 内に Button が見つかりません。");
                Destroy(currentUIInstance);
                return false;
            }

            // 4. カーソルの準備
            SetupCursor();

            // 5. 状態初期化
            currentIndex = 0;
            windowStart = 0;
            currentCallback = onBranchDecided;

            // 6. 初回描画
            RefreshSlots(playSE: false);

            // 7. 入力監視コルーチン開始
            if (inputCoroutine != null) StopCoroutine(inputCoroutine);
            inputCoroutine = StartCoroutine(InputLoopRoutine());

            return true;
        }

        /// <summary>
        /// カーソルオブジェクトを準備（未指定時はPrefab内検索または自動生成）
        /// </summary>
        private void SetupCursor()
        {
            if (cursorObject != null)
            {
                spawnedCursor = Instantiate(cursorObject, currentUIInstance.transform);
                spawnedCursor.SetActive(true);
                return;
            }

            // Prefab内の "Cursor" オブジェクトを検索
            Transform existingCursor = currentUIInstance.transform.Find("Cursor");
            if (existingCursor != null)
            {
                spawnedCursor = existingCursor.gameObject;
                spawnedCursor.SetActive(true);
                return;
            }

            // なければデフォルトカーソル（TMPの▶）を生成
            GameObject autoCursor = new GameObject("ScrollChoiceCursor", typeof(RectTransform));
            autoCursor.transform.SetParent(currentUIInstance.transform, false);
            var rect = autoCursor.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(30f, 30f);

            var tmp = autoCursor.AddComponent<TextMeshProUGUI>();
            tmp.text = "▶";
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            spawnedCursor = autoCursor;
            spawnedCursor.SetActive(true);
        }

        /// <summary>
        /// スロットのテキスト、ボタンリスナー、カーソル位置を更新（ウィンドウシフト）
        /// </summary>
        private void RefreshSlots(bool playSE)
        {
            if (playSE && !string.IsNullOrEmpty(cursorSEKey))
            {
                DialogueAudioManager.Instance?.PlaySE(cursorSEKey);
            }

            int slotCount = slotButtons.Count;
            int totalChoices = validChoices.Count;

            for (int i = 0; i < slotCount; i++)
            {
                Button btn = slotButtons[i];
                if (btn == null) continue;

                int choiceIdx = windowStart + i;

                if (choiceIdx < totalChoices)
                {
                    btn.gameObject.SetActive(true);

                    // テキスト更新
                    var choice = validChoices[choiceIdx];
                    var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text = choice.choiceText;
                    }
                    else
                    {
                        var txt = btn.GetComponentInChildren<Text>();
                        if (txt != null) txt.text = choice.choiceText;
                    }

                    // クリックリスナー設定
                    int targetChoiceIdx = choiceIdx;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        SelectChoice(targetChoiceIdx);
                    });

                    // マウスホバーでカーソル同期
                    SetupHoverEvent(btn.gameObject, targetChoiceIdx);

                    // 選択状態のビジュアル反映
                    bool isSelected = (choiceIdx == currentIndex);

                    // スプライト
                    var img = btn.GetComponent<Image>();
                    if (img != null)
                    {
                        if (isSelected && selectedSprite != null) img.sprite = selectedSprite;
                        else if (!isSelected && normalSprite != null) img.sprite = normalSprite;
                    }

                    // テキストカラー
                    if (tmp != null) tmp.color = isSelected ? selectedTextColor : normalTextColor;

                    // カーソル位置を更新
                    if (isSelected && spawnedCursor != null)
                    {
                        spawnedCursor.transform.position = btn.transform.position + (Vector3)cursorOffset;
                    }
                }
                else
                {
                    // 選択肢数がスロット数未満の場合、余りスロットは非表示
                    btn.gameObject.SetActive(false);
                }
            }

            // EventSystem の選択状態も同期
            int activeSlotIdx = currentIndex - windowStart;
            if (activeSlotIdx >= 0 && activeSlotIdx < slotButtons.Count && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(slotButtons[activeSlotIdx].gameObject);
            }
        }

        /// <summary>
        /// 下移動（1,2,3 -> 2,3,4 のウィンドウシフト ＆ 末尾から先頭へのループ）
        /// </summary>
        public void MoveDown()
        {
            int total = validChoices.Count;
            int slotCount = slotButtons.Count;
            if (total <= 1) return;

            if (currentIndex < total - 1)
            {
                currentIndex++;

                // スロット枠の下端を超えたらウィンドウを1つ下へシフト (1,2,3 -> 2,3,4)
                if (currentIndex >= windowStart + slotCount)
                {
                    windowStart = currentIndex - slotCount + 1;
                }
            }
            else if (loopNavigation)
            {
                // 一番下からさらに下 -> 一番上（先頭）へループ！
                currentIndex = 0;
                windowStart = 0;
            }

            RefreshSlots(playSE: true);
        }

        /// <summary>
        /// 上移動（先頭から末尾へのループ ＆ ウィンドウシフト）
        /// </summary>
        public void MoveUp()
        {
            int total = validChoices.Count;
            int slotCount = slotButtons.Count;
            if (total <= 1) return;

            if (currentIndex > 0)
            {
                currentIndex--;

                // スロット枠の上端を超えたらウィンドウを1つ上へシフト
                if (currentIndex < windowStart)
                {
                    windowStart = currentIndex;
                }
            }
            else if (loopNavigation)
            {
                // 一番上からさらに上 -> 一番下（末尾）へループ！
                currentIndex = total - 1;
                windowStart = Mathf.Max(0, total - slotCount);
            }

            RefreshSlots(playSE: true);
        }

        /// <summary>
        /// 選択決定
        /// </summary>
        private void SelectChoice(int index)
        {
            if (index < 0 || index >= validChoices.Count) return;

            if (!string.IsNullOrEmpty(submitSEKey))
            {
                DialogueAudioManager.Instance?.PlaySE(submitSEKey);
            }

            string nextNodeID = validChoices[index].targetNodeID;

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

            slotButtons.Clear();
            validChoices.Clear();
            spawnedCursor = null;
            currentCallback = null;
        }

        private void SetupHoverEvent(GameObject btnObj, int choiceIdx)
        {
            var trigger = btnObj.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btnObj.AddComponent<EventTrigger>();

            trigger.triggers.Clear();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener((_) =>
            {
                if (currentIndex != choiceIdx)
                {
                    currentIndex = choiceIdx;
                    RefreshSlots(playSE: true);
                }
            });
            trigger.triggers.Add(entry);
        }

        private IEnumerator InputLoopRoutine()
        {
            yield return null; // 初期フレームの入力誤爆防止

            float verticalTimer = 0f;

            while (true)
            {
                float v = GetVerticalInput();

                if (Mathf.Abs(v) > 0.5f)
                {
                    if (verticalTimer <= 0f)
                    {
                        if (v < 0f) MoveDown();
                        else if (v > 0f) MoveUp();

                        verticalTimer = repeatDelay;
                    }
                    else
                    {
                        verticalTimer -= Time.unscaledDeltaTime;
                    }
                }
                else
                {
                    verticalTimer = 0f;
                }

                if (GetSubmitInput())
                {
                    SelectChoice(currentIndex);
                    yield break;
                }

                yield return null;
            }
        }

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
            return Input.GetAxisRaw("Vertical");
        }

        private bool GetSubmitInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame ||
                    kb.spaceKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame)
                {
                    return true;
                }
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.buttonSouth.wasPressedThisFrame) return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F);
        }

        private Transform FindRootCanvas()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            return canvas != null ? canvas.transform : null;
        }
    }
}
