using System.Collections.Generic;

namespace Runtime.Dialogue.Core
{
    public interface IDialogueLayoutHandler
    {
        int Priority { get; }
        bool TryHandleLayout(string layoutName, Dictionary<string, string> args);
    }
}