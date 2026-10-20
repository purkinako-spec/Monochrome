using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Runtime.Dialogue.Logic;
using Runtime.Dialogue.Core;
using TMPro;

namespace Runtime.Dialogue
{
    public class DialogueTesting : MonoBehaviour
    {
        [Header("Dialogue Test Settings")]
        [SerializeField] private DialogueContainer testData;
        [SerializeField] private DialogueViewWindow dialogueView;

        [Header("Backlog UI Test Settings")]
        [Tooltip("バックログ全体を格納するUIパネル（Lキーで表示/非表示を切り替え）")]
        [SerializeField] private GameObject backlogPanel;

        [Tooltip("ScrollRect の Content (RectTransform)")]
        [SerializeField] private Transform logContentParent;

        [Tooltip("ログ1件分のテキスト表示用プレハブ")]
        [SerializeField] private GameObject logItemPrefab;

        [SerializeField] private bool autoStartForTest = true;

        private void Start()
        {
            if (backlogPanel != null)
            {
                backlogPanel.SetActive(false);
            }

            if (autoStartForTest)
            {
                StartCoroutine(AutoTestRoutine());
            }
        }

        private System.Collections.IEnumerator AutoTestRoutine()
        {
            yield return new WaitForSeconds(1.0f);
            if (DialogueManager.Instance != null && testData != null)
            {
                if (dialogueView != null)
                {
                    DialogueManager.Instance.RegisterView(dialogueView);
                }

                Debug.Log(">>> [EffectAutoTest] 会話テスト自動開始 <<<");
                DialogueManager.Instance.StartDialogue(testData, () => {
                    Debug.Log(">>> [EffectAutoTest] 全演出コマンドテスト完了 <<<");
                });

                // 各ノードの演出（1秒〜2秒）を見届けながら自動で次に進める
                for (int i = 0; i < 7; i++)
                {
                    yield return new WaitForSeconds(2.0f);
                    Debug.Log($">>> [EffectAutoTest] ノード進行 ({i + 1}/7) <<<");
                    DialogueManager.Instance.HandleAdvanceInput();
                }
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                if (DialogueManager.Instance != null && DialogueManager.Instance.CurrentState == DialogueState.Idle)
                {
                    if (dialogueView != null)
                    {
                        DialogueManager.Instance.RegisterView(dialogueView);
                    }

                    DialogueManager.Instance.StartDialogue(testData, () => {
                        Debug.Log("【テスト】会話イベントが終了しました。");
                    });
                }
            }

            if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
            {
                ToggleBacklogUI();
            }
        }

        private void ToggleBacklogUI()
        {
            if (backlogPanel == null) return;

            bool nextState = !backlogPanel.activeSelf;
            backlogPanel.SetActive(nextState);

            if (nextState)
            {
                RefreshLogUI();
            }
        }

        private void RefreshLogUI()
        {
            if (logContentParent == null || logItemPrefab == null || DialogueLogManager.Instance == null) return;

            foreach (Transform child in logContentParent)
            {
                Destroy(child.gameObject);
            }

            IReadOnlyList<DialogueLogData> logs = DialogueLogManager.Instance.Logs;
            foreach (var log in logs)
            {
                GameObject itemObj = Instantiate(logItemPrefab, logContentParent);

                // 子オブジェクトから名前欄(NameText)と本文欄(BodyText)を検索して割り当て
                SetTextValue(itemObj, "NameText", log.speakerName);
                SetTextValue(itemObj, "BodyText", log.dialogueText);
            }
        }

        /// <summary>
        /// プレハブ内の指定オブジェクトから TextMeshProUGUI または Text を取得して値を割り当てます
        /// </summary>
        private void SetTextValue(GameObject parent, string objectName, string value)
        {
            Transform targetTransform = parent.transform.Find(objectName);
            if (targetTransform == null)
            {
                // 直下になければ全孫要素から検索
                foreach (var child in parent.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == objectName)
                    {
                        targetTransform = child;
                        break;
                    }
                }
            }

            if (targetTransform == null) return;

            // TextMeshProUGUI を優先して割り当て
            var tmpText = targetTransform.GetComponent<TextMeshProUGUI>();
            if (tmpText != null)
            {
                tmpText.text = value ?? string.Empty;
                return;
            }

            // 旧 UI Text へのフォールバック
            var uiText = targetTransform.GetComponent<Text>();
            if (uiText != null)
            {
                uiText.text = value ?? string.Empty;
            }
        }
    }
}