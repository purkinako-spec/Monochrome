using System;
using System.Collections.Generic;
using Runtime.Dialogue.Core;

// 👇 分岐ハンドラーのインターフェースなので Branching 名前空間に配置する
namespace Runtime.Dialogue.Branching
{
    public interface IDialogueBranchHandler
    {
        int Priority { get; }
        bool TryHandleBranch(List<ChoiceData> choices, Action<string> onBranchDecided);
    }
}