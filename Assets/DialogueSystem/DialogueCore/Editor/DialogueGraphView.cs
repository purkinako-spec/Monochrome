using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Runtime.Dialogue.Core;

namespace DialogueSystem.Editor
{
    public class DialogueGraphView : GraphView
    {
        public DialogueGraphView()
        {
            SetupGridBackground();

            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            SetupContextMenu();
        }

        private void SetupGridBackground()
        {
            var gridBackground = new GridBackground();
            gridBackground.StretchToParentSize();
            Insert(0, gridBackground);
        }

        private void SetupContextMenu()
        {
            this.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
            {
                evt.menu.AppendAction("Create Node", (menuAction) =>
                {
                    // 目標3: ズレを直すため、GraphView内の正しい座標変換を使用する
                    Vector2 graphPosition = contentViewContainer.WorldToLocal(menuAction.eventInfo.mousePosition);
                    CreateNode("New Dialogue Node", graphPosition);
                });
            });
        }

        public void CreateNode(string nodeName, Vector2 position)
        {
            var nodeData = new DialogueNode
            {
                // 目標1: 初期IDを短く分かりやすい形式 (例: Node_a1b2c) にする
                nodeID = "Node_" + Guid.NewGuid().ToString().Substring(0, 5),
                dialogueText = "ここにセリフを入力",
                graphPosition = position
            };

            var nodeView = new DialogueNodeView(nodeData);
            nodeView.SetPosition(new Rect(position, new Vector2(250, 200)));

            AddElement(nodeView);
        }

        public DialogueNodeView CreateNodeView(DialogueNode nodeData)
        {
            var nodeView = new DialogueNodeView(nodeData);
            nodeView.SetPosition(new Rect(nodeData.graphPosition, new Vector2(250, 200)));
            AddElement(nodeView);
            return nodeView;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            ports.ForEach(port =>
            {
                if (startPort != port && startPort.node != port.node && startPort.direction != port.direction)
                {
                    compatiblePorts.Add(port);
                }
            });
            return compatiblePorts;
        }

        /// <summary>
        /// グラフ内の全要素（ノード・エッジ等）を安全に削除するユーティリティ
        /// </summary>
        public void ClearGraph()
        {
            var elements = this.graphElements.ToList();
            foreach (var e in elements)
            {
                try
                {
                    this.RemoveElement(e);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DialogueGraphView.ClearGraph] RemoveElement failed: {ex}");
                }
            }
        }

        /// <summary>
        /// ノードの表示更新やレイアウトを強制する補助（ロード後に呼ぶ）
        /// </summary>
        public void RefreshAll()
        {
            var nodeViews = this.graphElements.ToList().OfType<DialogueNodeView>().ToList();
            foreach (var nv in nodeViews)
            {
                try
                {
                    nv.RefreshPorts();
                    nv.RefreshExpandedState();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DialogueGraphView.RefreshAll] node refresh failed: {ex}");
                }
            }
            this.MarkDirtyRepaint();
        }
    }
}
