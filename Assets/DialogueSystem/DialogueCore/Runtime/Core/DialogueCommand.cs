using System.Collections.Generic;

namespace Runtime.Dialogue
{
    public class DialogueCommand
    {
        public string CommandName { get; private set; }
        public Dictionary<string, string> Arguments { get; private set; }
        public int CharacterIndex { get; set; }
        public bool IsExecuted { get; set; }

        public DialogueCommand(string commandName, Dictionary<string, string> arguments, int characterIndex = -1)
        {
            CommandName = commandName.ToLower().Trim();
            Arguments = arguments ?? new Dictionary<string, string>();
            CharacterIndex = characterIndex;
            IsExecuted = false;
        }

        // --- 追加：パラメータを安全に取り出すための便利メソッド ---
        public string GetString(string key, string defaultValue = "")
        {
            return Arguments.TryGetValue(key, out string val) ? val : defaultValue;
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (Arguments.TryGetValue(key, out string val) && float.TryParse(val, out float result)) return result;
            return defaultValue;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (Arguments.TryGetValue(key, out string val) && int.TryParse(val, out int result)) return result;
            return defaultValue;
        }
    }
}