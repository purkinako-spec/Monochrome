using System;
using System.Collections.Generic;
using UnityEngine;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Logic
{
    public class DialoguePortraitDispatcher : MonoBehaviour
    {
        public static DialoguePortraitDispatcher Instance { get; private set; }

        private List<IDialoguePortraitHandler> handlers = new List<IDialoguePortraitHandler>();

        private void Awake()
        {
            if (Instance == null) Instance = this;

            // éqóvëfÇé©ìÆìIÇ…åüçıÇµÇƒìoò^Ç∑ÇÈ
            var foundHandlers = GetComponentsInChildren<IDialoguePortraitHandler>();
            foreach (var h in foundHandlers)
            {
                RegisterHandler(h);
            }
        }

        public void RegisterHandler(IDialoguePortraitHandler handler)
        {
            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
                handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
        }

        public bool TryHandlePortrait(string targetID, string expression, string position, Dictionary<string, string> args, Action onComplete)
        {
            foreach (var handler in handlers)
            {
                if (handler.TryHandlePortrait(targetID, expression, position, args, onComplete)) return true;
            }
            return false;
        }

        public void ForceCompletePortrait(string targetID, string expression, string position, Dictionary<string, string> args)
        {
            foreach (var handler in handlers)
            {
                handler.ForceCompletePortrait(targetID, expression, position, args);
            }
        }
    }
}