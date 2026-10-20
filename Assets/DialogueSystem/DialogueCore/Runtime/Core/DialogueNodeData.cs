using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Dialogue.Core
{
    public enum ConditionOperator
    {
        GreaterOrEqual, // >=
        LessOrEqual,    // <=
        Equal,          // ==
        Greater,        // >
        Less            // <
    }

    [Serializable]
    public class DialogueNode
    {
        public Vector2 graphPosition;
        public string nextNodeID;

        public string nodeID;
        public string speakerName;
        public string dialogueText;
        public List<ChoiceData> choices = new List<ChoiceData>();
    }

    [Serializable]
    public class ChoiceData
    {
        public string choiceText;
        public string targetNodeID;

        public string branchType = "DefaultChoice";

        public string conditionKey = "";
        public ConditionOperator conditionOperator = ConditionOperator.GreaterOrEqual;
        public int conditionValue = 0;
    }

    public enum BranchType
    {
        DefaultChoice,
        AutoBranch,
        SpecialUI,
        CustomUI
    }
}