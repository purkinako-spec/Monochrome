using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Runtime.Dialogue;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

namespace Runtime.Dialogue.Branching
{
    [HandlerInfo(
        description: @"標準的なダイアログUI（DialogueViewWindow）を用いて画面上にボタンを生成・表示する分岐ハンドラーです。
FlagManagerと連携した条件評価（conditionKey / conditionOperator / conditionValue）にも対応しています。",

        usage: @"【設定方法】
1. ノードエディタ上で選択肢の BranchType に「DefaultChoiceHandler」または「DefaultChoice」を指定します。
2. 標準UI（DialogueViewWindow）の choiceButtonPrefab を通じてボタンが動的に生成されます。

【フラグ条件付き選択肢の例】
・条件指定: conditionKey=""has_key"", conditionOperator=""=="", conditionValue=""1""
  (※FlagManagerで条件を満たしている場合のみ、画面にボタンが表示されます)"
    )]
    public class DefaultChoiceHandler : MonoBehaviour, IDialogueBranchHandler
    {
        public int Priority => 0; // 最低優先度（フォールバック用）

        private void Start()
        {
            if (DialogueBranchDispatcher.Instance != null)
                DialogueBranchDispatcher.Instance.RegisterHandler(this);
        }

        public bool TryHandleBranch(List<ChoiceData> choices, Action<string> onBranchDecided)
        {
            var view = DialogueManager.Instance.CurrentView;
            if (view != null)
            {
                // 1. まず branchType で絞り込む
                var targetChoices = choices.Where(c =>
                    c.branchType == nameof(DefaultChoiceHandler) ||
                    c.branchType == "DefaultChoice").ToList();

                if (targetChoices.Count == 0) return false;

                // 2. FlagManager による条件評価でさらにフィルタリングする
                var displayChoices = new List<ChoiceData>();
                foreach (var choice in targetChoices)
                {
                    // 条件が設定されていない、または条件を満たしている場合のみ有効とする
                    if (string.IsNullOrEmpty(choice.conditionKey) ||
                        (FlagManager.Instance != null && FlagManager.Instance.EvaluateCondition(choice.conditionKey, choice.conditionOperator, choice.conditionValue)))
                    {
                        displayChoices.Add(choice);
                    }
                }

                // もし画面に出せる選択肢が1つもない場合は、このハンドラーでは処理できないとして false を返す
                if (displayChoices.Count == 0) return false;

                // 抽出したリストだけをUIに渡してボタンを作る
                view.ShowChoices(displayChoices, (selectedIndex) =>
                {
                    view.HideChoices();
                    // 選ばれたボタンの遷移先IDを取得して進行
                    string nextID = displayChoices[selectedIndex].targetNodeID;
                    onBranchDecided?.Invoke(nextID);
                });
                return true;
            }
            return false;
        }
    }
}