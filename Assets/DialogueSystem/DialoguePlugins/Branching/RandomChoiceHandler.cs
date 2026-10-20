using System; // Action に必要
using System.Collections.Generic;
using UnityEngine;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Branching;

namespace Runtime.Dialogue.Branching
{
    // 💡 クラス名を変更するだけで、Editorのドロップダウンに「RandomChoiceHandler」が自動追加されます
    [HandlerInfo(description: "設定された複数の選択肢の中からランダムで1つを自動的に選び、該当するノードへ分岐します。", usage: "ノードエディタ上で、選択肢のBranchTypeに「RandomChoiceHandler」を指定してください。")]
    public class RandomChoiceHandler : MonoBehaviour, IDialogueBranchHandler
    {
        public int Priority => 50;

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

            // ▼ 自動化ポイント: 自身のクラス名とエディタで選ばれた名前が一致するか判定
            if (choices[0].branchType != this.GetType().Name) return false;

            // ==========================================
            // ▼ ここに追加：ランダム分岐処理
            // ==========================================

            // 選択肢（choices）の中から、ランダムで1つ選ぶ
            int randomIndex = UnityEngine.Random.Range(0, choices.Count);
            ChoiceData selectedChoice = choices[randomIndex];

            Debug.Log($"[RandomChoice] {choices.Count}つの選択肢から '{selectedChoice.choiceText}' が選ばれました。");

            // 選ばれた選択肢の遷移先ノードへ進む
            onBranchDecided?.Invoke(selectedChoice.targetNodeID);

            return true; // 処理を完了したので true を返す
        }
    }
}