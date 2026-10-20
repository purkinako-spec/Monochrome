using System.Collections.Generic;
using UnityEngine;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Logic
{
    public class DialogueLayoutDispatcher : MonoBehaviour
    {
        public static DialogueLayoutDispatcher Instance { get; private set; }

        private List<IDialogueLayoutHandler> handlers = new List<IDialogueLayoutHandler>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void RegisterHandler(IDialogueLayoutHandler handler)
        {
            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
                handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
        }

        public bool TryHandleLayout(string layoutName, Dictionary<string, string> args)
        {
            foreach (var handler in handlers)
            {
                if (handler.TryHandleLayout(layoutName, args)) return true;
            }
            return false;
        }
    }
}