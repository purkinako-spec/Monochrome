using System;

namespace Runtime.Dialogue.Core
{
    [Serializable]
    public class DialogueLogData
    {
        public string speakerName;
        public string dialogueText;
        public string voiceID;

        public DialogueLogData(string speakerName, string dialogueText, string voiceID = "")
        {
            this.speakerName = speakerName;
            this.dialogueText = dialogueText;
            this.voiceID = voiceID;
        }
    }
}