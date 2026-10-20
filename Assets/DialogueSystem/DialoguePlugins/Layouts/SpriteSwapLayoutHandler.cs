using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialoguePlugins.Layouts
{
    [HandlerInfo(
        description: "レイアウト名に対応するスプライト画像へ動的に差し替えます。",
        usage: "使い方: [layout:name=SpriteSwap, sprite=Character_Happy]"
    )]
    public class SpriteSwapLayoutHandler : MonoBehaviour, IDialogueLayoutHandler
    {
        public int Priority => 10;

        [Header("スプライト差し替え対象のUIコンポーネント")]
        [SerializeField] private UnityEngine.UI.Image targetImage;

        [Header("利用可能なスプライトのリスト（またはデータベース）")]
        [SerializeField] private List<SpriteMapping> spriteMappings = new List<SpriteMapping>();

        [System.Serializable]
        public struct SpriteMapping
        {
            public string key;
            public Sprite sprite;
        }

        private void Start()
        {
            // ディスパッチャーへ自分自身を自動登録
            if (DialogueLayoutDispatcher.Instance != null)
            {
                DialogueLayoutDispatcher.Instance.RegisterHandler(this);
            }
        }

        public bool TryHandleLayout(string layoutName, Dictionary<string, string> arguments)
        {
            // このハンドラーが処理すべきレイアウト名か判定（例: "SpriteSwap" または直接画像キーを指定）
            string spriteKey = layoutName;
            if (arguments != null && arguments.ContainsKey("sprite"))
            {
                spriteKey = arguments["sprite"];
            }

            var mapping = spriteMappings.Find(x => x.key.Equals(spriteKey, StringComparison.OrdinalIgnoreCase));
            if (mapping.sprite != null && targetImage != null)
            {
                targetImage.sprite = mapping.sprite;
                Debug.Log($"[SpriteSwapLayoutHandler] スプライトを '{spriteKey}' に差し替えました。");
                return true;
            }

            return false;
        }
    }
}