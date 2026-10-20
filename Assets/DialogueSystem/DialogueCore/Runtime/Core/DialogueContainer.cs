using System.Collections.Generic;
using UnityEngine;

// 👇 Core名前空間に統一
namespace Runtime.Dialogue.Core
{
    [CreateAssetMenu(fileName = "NewDialogueContainer", menuName = "Dialogue/Dialogue Container")]
    public class DialogueContainer : ScriptableObject
    {
        [Tooltip("この会話イベントの開始地点(エントリーポイント)となるノードのID")]
        public string startNodeID;

        [Tooltip("この会話イベントに含まれるすべてのノードのリスト")]
        public List<DialogueNode> allNodes = new List<DialogueNode>();

        private Dictionary<string, DialogueNode> nodeCache = new Dictionary<string, DialogueNode>();

        public DialogueNode GetNodeByID(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (nodeCache.Count > 0 && nodeCache.ContainsKey(id))
            {
                return nodeCache[id];
            }

            nodeCache.Clear();
            foreach (var node in allNodes)
            {
                if (node != null && !nodeCache.ContainsKey(node.nodeID))
                {
                    nodeCache.Add(node.nodeID, node);
                }
            }

            return nodeCache.ContainsKey(id) ? nodeCache[id] : null;
        }

        private void OnEnable()
        {
            nodeCache.Clear();
        }
    }
}