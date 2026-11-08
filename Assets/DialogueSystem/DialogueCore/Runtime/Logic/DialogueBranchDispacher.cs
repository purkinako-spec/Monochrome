using System;
using System.Collections.Generic;
using UnityEngine;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Branching
{
    [AddComponentMenu("Dialogue/Core/Dialogue Branch Dispatcher")]
    [DisallowMultipleComponent]
    public class DialogueBranchDispatcher : MonoBehaviour
    {
        private static DialogueBranchDispatcher instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        public static DialogueBranchDispatcher Instance
        {
            get
            {
                if (instance == null || !instance)
                {
                    instance = FindFirstObjectByType<DialogueBranchDispatcher>();
                    if ((instance == null || !instance) && Application.isPlaying)
                    {
                        var go = new GameObject("[DialogueBranchDispatcher]");
                        instance = go.AddComponent<DialogueBranchDispatcher>();
                        if (go.transform.parent == null) DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        private readonly List<IDialogueBranchHandler> handlers = new List<IDialogueBranchHandler>();

        private void Awake()
        {
            if (instance == null || !instance)
            {
                instance = this;
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void RegisterHandler(IDialogueBranchHandler handler)
        {
            if (handler != null && !handlers.Contains(handler))
            {
                handlers.Add(handler);
                handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // 優先度ソート
            }
        }

        public void UnregisterHandler(IDialogueBranchHandler handler)
        {
            if (handler != null && handlers.Contains(handler))
            {
                handlers.Remove(handler);
            }
        }

        public bool TryHandleBranch(List<ChoiceData> choices, Action<string> onBranchDecided)
        {
            // 破棄されたハンドラーを掃除しながら処理
            for (int i = handlers.Count - 1; i >= 0; i--)
            {
                var h = handlers[i];
                if (h == null || (h is UnityEngine.Object obj && !obj))
                {
                    handlers.RemoveAt(i);
                    continue;
                }
            }

            foreach (var handler in handlers)
            {
                if (handler != null && handler.TryHandleBranch(choices, onBranchDecided))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
