using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Runtime.Dialogue.Core;

namespace DialogueSystem.Editor
{
    public static class DialogueGraphViewIO
    {
        public static void Save(DialogueGraphView graphView, DialogueContainer container)
        {
            Debug.Log("[DialogueGraphViewIO] Save start");
            container.allNodes.Clear();

            List<Edge> edges = graphView.graphElements.ToList().OfType<Edge>().ToList();
            List<DialogueNodeView> nodeViews = graphView.graphElements.ToList().OfType<DialogueNodeView>().ToList();

            foreach (DialogueNodeView nodeView in nodeViews)
            {
                DialogueNode nodeData = nodeView.NodeData;
                nodeData.graphPosition = nodeView.GetPosition().position;
                nodeData.nextNodeID = string.Empty;

                for (int i = 0; i < nodeData.choices.Count; i++)
                {
                    var choice = nodeData.choices[i];
                    choice.targetNodeID = string.Empty;
                    nodeData.choices[i] = choice;
                }

                var outputPorts = nodeView.outputContainer.Query<Port>().ToList();
                foreach (var port in outputPorts)
                {
                    Edge connectedEdge = edges.FirstOrDefault(edge => edge.output == port);
                    if (connectedEdge != null && connectedEdge.input != null)
                    {
                        if (connectedEdge.input.node is DialogueNodeView targetNodeView)
                        {
                            DialogueNode targetData = targetNodeView.NodeData;

                            if (port.userData is int choiceIndex && choiceIndex < nodeData.choices.Count)
                            {
                                var choice = nodeData.choices[choiceIndex];
                                choice.targetNodeID = targetData.nodeID;
                                nodeData.choices[choiceIndex] = choice;
                            }
                            else if (port.userData == null)
                            {
                                nodeData.nextNodeID = targetData.nodeID;
                            }
                        }
                    }
                }

                container.allNodes.Add(nodeData);
            }

            if (container.allNodes.Count > 0)
            {
                var startNode = container.allNodes.OrderBy(n => n.graphPosition.x).First();
                container.startNodeID = startNode.nodeID;
            }

            Debug.Log($"[DialogueGraphViewIO] Save finished. nodes saved: {container.allNodes.Count}");
        }

        public static void Load(DialogueGraphView graphView, DialogueContainer container)
        {
            Debug.Log("[DialogueGraphViewIO] Load start");
            if (container.allNodes == null || container.allNodes.Count == 0)
            {
                Debug.LogWarning("[DialogueGraphViewIO] Load aborted: container has no nodes.");
                return;
            }

            var viewCache = new Dictionary<string, DialogueNodeView>();

            foreach (DialogueNode nodeData in container.allNodes)
            {
                Debug.Log($"[DialogueGraphViewIO] Creating node view for ID={nodeData.nodeID} pos={nodeData.graphPosition}");
                var nodeView = graphView.CreateNodeView(nodeData);
                viewCache.Add(nodeData.nodeID, nodeView);

            }

            foreach (DialogueNode nodeData in container.allNodes)
            {
                if (!viewCache.TryGetValue(nodeData.nodeID, out DialogueNodeView sourceView)) continue;

                if (!string.IsNullOrEmpty(nodeData.nextNodeID) && viewCache.TryGetValue(nodeData.nextNodeID, out DialogueNodeView targetView))
                {
                    Port outputPort = sourceView.outputContainer.Children().OfType<Port>().FirstOrDefault(p => p.userData == null);
                    Port inputPort = targetView.inputContainer.Q<Port>();

                    if (outputPort != null && inputPort != null)
                    {
                        Edge edge = outputPort.ConnectTo(inputPort);
                        graphView.AddElement(edge);
                    }
                }

                if (nodeData.choices != null && nodeData.choices.Count > 0)
                {
                    var choicePorts = sourceView.outputContainer.Query<Port>().ToList();
                    for (int i = 0; i < nodeData.choices.Count; i++)
                    {
                        string targetID = nodeData.choices[i].targetNodeID;
                        if (!string.IsNullOrEmpty(targetID) && viewCache.TryGetValue(targetID, out DialogueNodeView branchTargetView))
                        {
                            Port outputPort = choicePorts.FirstOrDefault(p => p.userData is int idx && idx == i);
                            Port inputPort = branchTargetView.inputContainer.Q<Port>();

                            if (outputPort != null && inputPort != null)
                            {
                                Edge edge = outputPort.ConnectTo(inputPort);
                                graphView.AddElement(edge);
                            }
                        }
                    }
                }
            }

            Debug.Log($"[DialogueGraphViewIO] Load finished. nodes loaded: {viewCache.Count}");
            graphView.RefreshAll();
            try { graphView.FrameAll(); } catch { /* ignore */ }
        }
    }
}
