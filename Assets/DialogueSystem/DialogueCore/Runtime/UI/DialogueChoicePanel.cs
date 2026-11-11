using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Audio;

namespace Runtime.Dialogue.UI
{
    /// <summary>
    /// 選択肢をスクロールさせずに表示枠（Prefab上のボタン数）単位で
    /// 1,2,3 -> 2,3,4 のようにウィンドウシフトして切り替える選択肢コントローラー。
    /// - 先頭・末尾でのループ（ラップアラウンド）
    /// - 選択中スロットへのカーソル（Cursor）追従表示
    /// - キーボード / ゲームパッド / マウス操作対応
    /// </summary>
    public class DialogueChoicePanel : MonoBehaviour
    {
        [Header("Button Slots")]
        [Tooltip("表示枠となるボタンスロットのリスト。未設定時は子要素の Button を自動収集します。")]
        [SerializeField] private List<Button> buttonSlots = new List<Button>();

        [Header("Cursor Settings")]
        [Tooltip("選択中のボタンを指すカーソルオブジェクト（未設定時は自動生成されます）")]
        [SerializeField] private RectTransform cursor;

        [Tooltip("ボタン中心/左端からのカーソル表示オフセット")]
        [SerializeField] private Vector2 cursorOffset = new Vector2(-20f, 0f);

        [Header("Audio Settings")]
        [SerializeField] private string cursorMoveSEKey = "cursor_move";
        [SerializeField] private string choiceSelectSEKey = "choice_select";

        // 状態変数
        private List<ChoiceData> choices = new List<ChoiceData>();
        private Action<int> onSelectedCallback;
        private int selectedIndex = 0;   // 全体リスト内での選択インデックス (0 .. N-1)
        private int windowStart = 0;     // 現在スロットに表示している先頭インデックス (0 .. N-M)
        private bool isListeningInput = false;

        private void Awake()
        {
            CollectButtonSlots();
            EnsureCursor();
        }

        private void OnEnable()
        {
            isListeningInput = true;
        }

        private void OnDisable()
        {
            isListeningInput = false;
        }

        /// <summary>
        /// ボタンスロットを自動収集（未設定時）
        /// </summary>
        private void CollectButtonSlots()
        {
            if (buttonSlots == null || buttonSlots.Count == 0)
            {
                buttonSlots = new List<Button>(GetComponentsInChildren<Button>(true));
                // 画面上の上から下（Y座標降順）に自動ソート
                buttonSlots.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
            }
        }

        /// <summary>
        /// カーソルオブジェクトを確認・自動生成
        /// </summary>
        private void EnsureCursor()
        {
            if (cursor != null) return;

            // 子要素から "Cursor" という名前のオブジェクトを検索
            var found = transform.Find("Cursor");
            if (found != null)
            {
                cursor = found as RectTransform;
                return;
            }

            // 見つからない場合は視覚的なデフォルトカーソル（TMPの▶）を生成
            var cursorObj = new GameObject("ChoiceCursor", typeof(RectTransform));
            cursorObj.transform.SetParent(transform, false);
            cursor = cursorObj.GetComponent<RectTransform>();
            cursor.sizeDelta = new Vector2(30f, 30f);

            var tmp = cursorObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "▶";
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }

        /// <summary>
        /// 選択肢の表示をセットアップして開始します
        /// </summary>
        public void Setup(List<ChoiceData> choiceList, Action<int> onSelect)
        {
            CollectButtonSlots();
            EnsureCursor();

            choices = choiceList ?? new List<ChoiceData>();
            onSelectedCallback = onSelect;
            selectedIndex = 0;
            windowStart = 0;
            isListeningInput = true;

            if (choices.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            RefreshView();
        }

        /// <summary>
        /// 現在の windowStart と selectedIndex に基づいてUIを更新します
        /// </summary>
        public void RefreshView()
        {
            if (buttonSlots == null || buttonSlots.Count == 0 || choices == null || choices.Count == 0)
                return;

            int slotCount = buttonSlots.Count;
            int totalChoices = choices.Count;

            // 各スロットのテキストとクリックイベントを設定
            for (int slotIdx = 0; slotIdx < slotCount; slotIdx++)
            {
                var btn = buttonSlots[slotIdx];
                if (btn == null) continue;

                int choiceIdx = windowStart + slotIdx;

                if (choiceIdx < totalChoices)
                {
                    btn.gameObject.SetActive(true);

                    // テキスト設定
                    var textComp = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (textComp != null)
                    {
                        textComp.text = choices[choiceIdx].choiceText;
                    }

                    // リスナー再バインド
                    btn.onClick.RemoveAllListeners();
                    int targetChoiceIdx = choiceIdx;
                    btn.onClick.AddListener(() =>
                    {
                        ConfirmSelection(targetChoiceIdx);
                    });

                    // ホバーイベント（マウス対応）
                    SetupEventTrigger(btn.gameObject, targetChoiceIdx);
                }
                else
                {
                    // 選択肢数がスロット数より少ない場合、余ったスロットは非表示
                    btn.gameObject.SetActive(false);
                }
            }

            UpdateCursorPosition();
        }

        /// <summary>
        /// カーソルを選択中のスロットの位置へ移動します
        /// </summary>
        private void UpdateCursorPosition()
        {
            if (cursor == null || buttonSlots == null || buttonSlots.Count == 0) return;

            int activeSlotIdx = selectedIndex - windowStart;
            if (activeSlotIdx >= 0 && activeSlotIdx < buttonSlots.Count)
            {
                var targetSlot = buttonSlots[activeSlotIdx];
                if (targetSlot != null && targetSlot.gameObject.activeInHierarchy)
                {
                    cursor.gameObject.SetActive(true);

                    // カーソルの位置をボタンの左側に追従
                    var slotRect = targetSlot.GetComponent<RectTransform>();
                    if (slotRect != null)
                    {
                        // ワールド座標で高さを合わせる
                        Vector3 worldPos = targetSlot.transform.position;
                        cursor.position = new Vector3(cursor.position.x, worldPos.y, worldPos.z);

                        // パネル内でのローカルX座標（ボタンの左端）
                        Vector3[] corners = new Vector3[4];
                        slotRect.GetWorldCorners(corners);
                        float leftX = corners[0].x;
                        cursor.position = new Vector3(leftX + cursorOffset.x, worldPos.y + cursorOffset.y, worldPos.z);
                    }

                    // UI EventSystem の選択状態も同期
                    if (EventSystem.current != null)
                    {
                        EventSystem.current.SetSelectedGameObject(targetSlot.gameObject);
                    }
                }
            }
        }

        private void Update()
        {
            if (!isListeningInput || choices == null || choices.Count == 0) return;

            // --- 下移動入力 ---
            if (IsDownPressed())
            {
                MoveDown();
            }
            // --- 上移動入力 ---
            else if (IsUpPressed())
            {
                MoveUp();
            }
            // --- 決定入力 ---
            else if (IsConfirmPressed())
            {
                ConfirmSelection(selectedIndex);
            }
        }

        /// <summary>
        /// 下方向へ移動（1,2,3 -> 2,3,4 のウィンドウシフト ＆ 末尾から先頭へのループ）
        /// </summary>
        public void MoveDown()
        {
            int total = choices.Count;
            int slotCount = buttonSlots.Count;
            if (total <= 1) return;

            if (selectedIndex < total - 1)
            {
                selectedIndex++;

                // スロット枠の下端を超えたらウィンドウを1つ下へシフト (例: 1,2,3 -> 2,3,4)
                if (selectedIndex >= windowStart + slotCount)
                {
                    windowStart = selectedIndex - slotCount + 1;
                }
            }
            else
            {
                // 末尾からさらに下 -> 一番上（先頭）へループ！
                selectedIndex = 0;
                windowStart = 0;
            }

            PlaySE(cursorMoveSEKey);
            RefreshView();
        }

        /// <summary>
        /// 上方向へ移動（先頭から末尾へのループ ＆ ウィンドウシフト）
        /// </summary>
        public void MoveUp()
        {
            int total = choices.Count;
            int slotCount = buttonSlots.Count;
            if (total <= 1) return;

            if (selectedIndex > 0)
            {
                selectedIndex--;

                // スロット枠の上端を超えたらウィンドウを1つ上へシフト
                if (selectedIndex < windowStart)
                {
                    windowStart = selectedIndex;
                }
            }
            else
            {
                // 一番上からさらに上 -> 一番下（末尾）へループ！
                selectedIndex = total - 1;
                windowStart = Mathf.Max(0, total - slotCount);
            }

            PlaySE(cursorMoveSEKey);
            RefreshView();
        }

        /// <summary>
        /// 選択肢を決定
        /// </summary>
        public void ConfirmSelection(int index)
        {
            if (index < 0 || index >= choices.Count) return;

            isListeningInput = false;
            PlaySE(choiceSelectSEKey);

            var cb = onSelectedCallback;
            onSelectedCallback = null;
            cb?.Invoke(index);
        }

        private void SetupEventTrigger(GameObject btnObj, int choiceIdx)
        {
            var trigger = btnObj.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btnObj.AddComponent<EventTrigger>();

            trigger.triggers.Clear();

            // マウスホバーでインデックスを更新
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener((_) =>
            {
                if (selectedIndex != choiceIdx)
                {
                    selectedIndex = choiceIdx;
                    UpdateCursorPosition();
                }
            });
            trigger.triggers.Add(entry);
        }

        private bool IsDownPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.downArrowKey.wasPressedThisFrame ||
                    Keyboard.current.sKey.wasPressedThisFrame)
                    return true;
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current.dpad.down.wasPressedThisFrame ||
                    Gamepad.current.leftStick.down.wasPressedThisFrame)
                    return true;
            }

            return false;
        }

        private bool IsUpPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.upArrowKey.wasPressedThisFrame ||
                    Keyboard.current.wKey.wasPressedThisFrame)
                    return true;
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current.dpad.up.wasPressedThisFrame ||
                    Gamepad.current.leftStick.up.wasPressedThisFrame)
                    return true;
            }

            return false;
        }

        private bool IsConfirmPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.enterKey.wasPressedThisFrame ||
                    Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                    Keyboard.current.spaceKey.wasPressedThisFrame ||
                    Keyboard.current.fKey.wasPressedThisFrame)
                    return true;
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current.buttonSouth.wasPressedThisFrame)
                    return true;
            }

            return false;
        }

        private void PlaySE(string seKey)
        {
            if (!string.IsNullOrEmpty(seKey))
            {
                DialogueAudioManager.Instance?.PlaySE(seKey);
            }
        }
    }
}
