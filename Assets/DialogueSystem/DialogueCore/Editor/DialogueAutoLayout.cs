using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Runtime.Dialogue.Core;

namespace DialogueSystem.Editor
{
    /// <summary>
    /// ノードグラフの自動ツリー整列を行うユーティリティ
    /// 親ノードから接続先ノードを層（階層）ごとに横並びまたは縦並びに配置します。
    /// </summary>
    public static class DialogueAutoLayout
    {
        private const float HorizontalSpacing = 320f;
        private const float VerticalSpacing = 220f;

        public static void Layout(DialogueGraphView graphView)
        {
            var nodeViews = graphView.graphElements.ToList().OfType<DialogueNodeView>().ToList();
            if (nodeViews.Count == 0) return;

            var edges = graphView.graphElements.ToList().OfType<Edge>().ToList();

            // 1. 各ノードの親と子の関係（有向グラフ）を構築
            Dictionary<string, DialogueNodeView> idToNode = new Dictionary<string, DialogueNodeView>();
            Dictionary<string, List<string>> childrenMap = new Dictionary<string, List<string>>();
            HashSet<string> targetNodes = new HashSet<string>();

            foreach (var nv in nodeViews)
            {
                string id = nv.NodeData.nodeID;
                idToNode[id] = nv;
                childrenMap[id] = new List<string>();
            }

            foreach (var edge in edges)
            {
                if (edge.output?.node is DialogueNodeView source && edge.input?.node is DialogueNodeView target)
                {
                    string srcId = source.NodeData.nodeID;
                    string dstId = target.NodeData.nodeID;
                    if (childrenMap.ContainsKey(srcId) && !childrenMap[srcId].Contains(dstId))
                    {
                        childrenMap[srcId].Add(dstId);
                    }
                    targetNodes.Add(dstId);
                }
            }

            // 2. ルートノード（親からの接続がないノード、またはX座標が一番左のノード）を特定
            List<string> rootNodeIds = idToNode.Keys.Where(id => !targetNodes.Contains(id)).ToList();
            if (rootNodeIds.Count == 0)
            {
                // サイクルのみの場合は一番左にあるノードをルートにする
                var leftmost = nodeViews.OrderBy(nv => nv.GetPosition().x).First();
                rootNodeIds.Add(leftmost.NodeData.nodeID);
            }

            // 3. BFS / DFS でレイヤー階層（X深度）を計算
            Dictionary<string, int> depthMap = new Dictionary<string, int>();
            HashSet<string> visited = new HashSet<string>();
            Queue<(string id, int depth)> queue = new Queue<(string id, int depth)>();

            foreach (var rootId in rootNodeIds)
            {
                queue.Enqueue((rootId, 0));
                depthMap[rootId] = 0;
                visited.Add(rootId);
            }

            while (queue.Count > 0)
            {
                var (currId, depth) = queue.Dequeue();
                foreach (var childId in childrenMap[currId])
                {
                    int nextDepth = depth + 1;
                    if (!depthMap.ContainsKey(childId) || depthMap[childId] < nextDepth)
                    {
                        depthMap[childId] = nextDepth;
                    }
                    if (!visited.Contains(childId))
                    {
                        visited.Add(childId);
                        queue.Enqueue((childId, nextDepth));
                    }
                }
            }

            // 孤立ノード（親も子もない）のケア
            foreach (var nv in nodeViews)
            {
                string id = nv.NodeData.nodeID;
                if (!depthMap.ContainsKey(id))
                {
                    depthMap[id] = 0;
                }
            }

            // 4. レイヤーごとにグループ化して Y 座標を均等配置
            var layerGroups = depthMap.GroupBy(kv => kv.Value).OrderBy(g => g.Key);
            float startX = 100f;
            float startY = 100f;

            foreach (var group in layerGroups)
            {
                int layer = group.Key;
                var nodesInLayer = group.Select(kv => idToNode[kv.Key]).ToList();

                float currentY = startY;
                float posX = startX + layer * HorizontalSpacing;

                foreach (var nv in nodesInLayer)
                {
                    Rect currentRect = nv.GetPosition();
                    nv.SetPosition(new Rect(posX, currentY, currentRect.width > 50 ? currentRect.width : 250f, currentRect.height > 50 ? currentRect.height : 200f));
                    nv.NodeData.graphPosition = new Vector2(posX, currentY);
                    currentY += VerticalSpacing;
                }
            }
        }
    }
}
