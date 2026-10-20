using System;
using System.Collections.Generic;
using UnityEngine;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Branching; // 追加：IDialogueBranchHandler や Dispatcher を参照

namespace DialoguePlugins.Branching
{
    [HandlerInfo(description: "プレイヤーの入力を待たずに、自動的に指定されたノードへ分岐します。", usage: "ノードエディタ上で、選択肢のBranchTypeに「AutoBranchHandler」を指定してください。")]
    public class AutoBranchHandler : MonoBehaviour, IDialogueBranchHandler
    {
        public int Priority => 100; // 優先度を高く設定（通常ボタンより先に判定させる）

        private void Start()
        {
            if (DialogueBranchDispatcher.Instance != null)
                DialogueBranchDispatcher.Instance.RegisterHandler(this);
        }

        public bool TryHandleBranch(List<ChoiceData> choices, Action<string> onBranchDecided)
        {
            foreach (var choice in choices)
            {
                // エディタ側の統一に合わせて "AutoBranchHandler"（旧: "AutoBranch"）および互換判定を行う
                if (choice.branchType == nameof(AutoBranchHandler) || choice.branchType == "AutoBranch")
                {
                    // 必要に応じてここで条件判定（例: 所持金チェックなど）を入れる
                    Debug.Log($"[AutoBranchHandler] 自動分岐を実行: {choice.choiceText}");

                    onBranchDecided?.Invoke(choice.targetNodeID);
                    return true;
                }
            }
            return false;
        }
    }
}