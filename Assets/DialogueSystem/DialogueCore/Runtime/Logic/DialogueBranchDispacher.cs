using System;
using System.Collections.Generic;
using UnityEngine;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Branching
{
    public class DialogueBranchDispatcher : MonoBehaviour
    {
        public static DialogueBranchDispatcher Instance { get; private set; }

        private List<IDialogueBranchHandler> handlers = new List<IDialogueBranchHandler>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void RegisterHandler(IDialogueBranchHandler handler)
        {
            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
                handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // 優先度が高い順にソート
            }
        }

        public bool TryHandleBranch(List<ChoiceData> choices, Action<string> onBranchDecided)
        {
            foreach (var handler in handlers)
            {
                if (handler.TryHandleBranch(choices, onBranchDecided))
                {
                    return true;
                }
            }
            return false;
        }
    }
}