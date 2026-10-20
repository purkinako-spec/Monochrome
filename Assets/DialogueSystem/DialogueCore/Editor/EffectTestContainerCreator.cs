using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Runtime.Dialogue.Core;

namespace DialogueSystem.Editor
{
    /// <summary>
    /// 演出コマンドテスト用 DialogueContainer を自動生成するエディタユーティリティ
    /// Menu: Dialogue/Create Effect Test Container
    /// </summary>
    public static class EffectTestContainerCreator
    {
        private const string SavePath = "Assets/DialogueSystem/Data/DialogueContainer/EffectCommandTest.asset";

        [MenuItem("Dialogue/Create Effect Test Container")]
        public static void CreateEffectTestContainer()
        {
            // 既存アセットがあれば上書き確認なしで削除
            if (File.Exists(Path.GetFullPath(SavePath)))
            {
                AssetDatabase.DeleteAsset(SavePath);
            }

            var container = ScriptableObject.CreateInstance<DialogueContainer>();

            // ノード定義ヘルパー
            var nodes = new List<DialogueNode>();

            // === ノード1: 開始 ===
            nodes.Add(MakeNode("n1", "n2", "System", 
                "【演出コマンドテスト開始】\n全部で5つのエフェクトをテストします。", 0));

            // === ノード2: fade_out テスト ===
            nodes.Add(MakeNode("n2", "n3", "System",
                "[fade_out:time=1.0]【fade_out テスト】\n画面が1秒かけてフェードアウトします。", 1));

            // === ノード3: fade_in テスト ===
            nodes.Add(MakeNode("n3", "n4", "System",
                "[fade_in:time=1.0]【fade_in テスト】\n暗転から1秒かけて明るくなります。", 2));

            // === ノード4: dialogue_fade テスト（アウト→インをコマンドで連続実行）===
            nodes.Add(MakeNode("n4", "n5", "System",
                "[dialogue_fade:type=out,time=0.5]【dialogue_fade テスト】\nダイアログ枠自体を0.5秒でフェードアウト後、再表示します。[dialogue_fade:type=in,time=0.5]", 3));

            // === ノード5: shake テスト ===
            nodes.Add(MakeNode("n5", "n6", "System",
                "[shake:target=window,magnitude=10,time=0.6,mode=pos]【shake テスト】\nダイアログウィンドウが揺れます。", 4));

            // === ノード6: vignette テスト ===
            nodes.Add(MakeNode("n6", "n7", "System",
                "[vignette:state=T,time=0.8,intensity=0.75]【vignette テスト】\n画面端が暗くなるビネット演出です（URP）。", 5));

            // === ノード7: vignette リセット → 終了 ===
            nodes.Add(MakeNode("n7", "", "System",
                "[vignette:state=F,time=0.5]【全テスト完了】\nビネットを解除しました。テスト終了。", 6));

            container.allNodes = nodes;
            container.startNodeID = "n1";

            // ディレクトリ保証
            string dir = Path.GetDirectoryName(SavePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            AssetDatabase.CreateAsset(container, SavePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // プロジェクトウィンドウでフォーカス
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = container;

            Debug.Log($"[EffectTestContainerCreator] テスト用コンテナを作成しました: {SavePath}");
            EditorUtility.DisplayDialog(
                "完了",
                $"テスト用コンテナを作成しました。\n{SavePath}\n\nGM/DialogueStarterのコンテナフィールドに設定し、Playモードで動作確認してください。",
                "OK"
            );
        }

        private static DialogueNode MakeNode(string id, string nextID, string speaker, string text, int xIndex)
        {
            return new DialogueNode
            {
                nodeID        = id,
                nextNodeID    = nextID,
                speakerName   = speaker,
                dialogueText  = text,
                graphPosition = new Vector2(xIndex * 300f, 0f),
                choices       = new List<ChoiceData>()
            };
        }
    }
}
