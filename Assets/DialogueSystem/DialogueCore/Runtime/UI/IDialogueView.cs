// ファイル: IDialogueView.cs
using System;
using System.Collections.Generic;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue
{
    public interface IDialogueView
    {
        void InitializeView();
        void CloseView();

        // 既存のシグネチャ
        void DisplaySentence(string speakerID, string cleanText, Action onTypingComplete);

        // ← ここを追加（コマンドリストを受け取るオーバーロード）
        void DisplaySentence(string speakerID, string cleanText, List<DialogueCommand> commands, Action onTypingComplete);

        void ForceCompleteTyping();
        void ShowChoices(List<ChoiceData> choices, Action<int> onChoiceSelected);
        void HideChoices();
    }
}
