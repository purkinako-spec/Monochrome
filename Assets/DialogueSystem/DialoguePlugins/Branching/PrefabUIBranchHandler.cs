using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;
using Runtime.Dialogue.Audio; // 👈 追加: SE再生用の参照

namespace Runtime.Dialogue.Branching
{
    [HandlerInfo(description: "事前に設定した独自のUIプレハブを生成して、カスタムデザインの選択肢画面を表示します。", usage: "ノードエディタ上で、選択肢のBranchTypeに「PrefabUIBranchHandler」を指定してください。また、Inspectorから『Branch UI Prefab』にボタンを含むプレハブを割り当てる必要があります。")]
    public class PrefabUIBranchHandler : MonoBehaviour, IDialogueBranchHandler
    {
        [Header("UI Settings")]
        [SerializeField] private GameObject branchUIPrefab;
        [SerializeField] private Transform uiParent;

        [Header("Audio Settings")]
        [SerializeField] private string defaultSEKey = "choice_select"; // 👈 追加: 決定音のキー

        public int Priority => 50;
        private GameObject currentUIInstance;

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

            var validChoices = choices.Where(c =>
                c.branchType == targetBranchType &&
                (FlagManager.Instance == null || FlagManager.Instance.EvaluateCondition(c.conditionKey, c.conditionOperator, c.conditionValue))
            ).ToList();

            if (validChoices.Count == 0) return false;

            if (branchUIPrefab == null)
            {
                Debug.LogError($"[{targetBranchType}] Branch UI Prefab が設定されていません。");
                return false;
            }

            currentUIInstance = uiParent != null ? Instantiate(branchUIPrefab, uiParent) : Instantiate(branchUIPrefab);
            Button[] availableButtons = currentUIInstance.GetComponentsInChildren<Button>(true);

            int processCount = Mathf.Min(validChoices.Count, availableButtons.Length);

            if (processCount == 0)
            {
                Destroy(currentUIInstance);
                return false;
            }

            for (int i = 0; i < availableButtons.Length; i++)
            {
                Button btn = availableButtons[i];
                if (i < processCount)
                {
                    var choice = validChoices[i];
                    btn.gameObject.SetActive(true);

                    var tmpText = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmpText != null)
                    {
                        tmpText.text = choice.choiceText;
                    }
                    else
                    {
                        var textComponent = btn.GetComponentInChildren<Text>();
                        if (textComponent != null) textComponent.text = choice.choiceText;
                    }

                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        // 💡 修正箇所: クリック時にSE（決定音）を鳴らす処理を追加
                        DialogueAudioManager.Instance?.PlaySE(defaultSEKey);

                        if (currentUIInstance != null) Destroy(currentUIInstance);
                        onBranchDecided?.Invoke(choice.targetNodeID);
                    });
                }
                else
                {
                    btn.gameObject.SetActive(false);
                }
            }
            return true;
        }
    }
}