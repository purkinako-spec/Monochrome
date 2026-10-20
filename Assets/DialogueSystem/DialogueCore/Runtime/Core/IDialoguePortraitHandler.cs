using System;
using System.Collections.Generic;

namespace Runtime.Dialogue.Core
{
    public interface IDialoguePortraitHandler
    {
        int Priority { get; }
        bool TryHandlePortrait(string targetID, string expression, string position, Dictionary<string, string> args, Action onComplete);
        void ForceCompletePortrait(string targetID, string expression, string position, Dictionary<string, string> args);
    }
}