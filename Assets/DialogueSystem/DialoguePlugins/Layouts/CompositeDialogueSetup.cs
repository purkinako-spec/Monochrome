using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Plugins.Portraits;

namespace Runtime.Dialogue.Plugins.Layouts
{
    /// <summary>
    /// 統合UI Prefab（DialogueUIWindow と PortraitRoot がセットになったPrefab）に
    /// 設定する参照ヘルパーコンポーネント。
    /// 未設定でも自動検出されますが、明示的に指定することで確実な動作が保証されます。
    /// </summary>
    [AddComponentMenu("Dialogue/Layout/Composite Dialogue Setup")]
    public class CompositeDialogueSetup : MonoBehaviour
    {
        [Header("Dialogue Window (必須)")]
        [Tooltip("このPrefabに含まれる DialogueViewWindow")]
        public DialogueViewWindow dialogueViewWindow;

        [Tooltip("背景スプライト差し替え（SpriteSwap）対象の Image コンポーネント（任意）")]
        public Image backgroundTargetImage;

        [Header("Fade (dialogue_fade対応)")]
        [Tooltip("dialogue_fadeコマンドでフェードさせるCanvasGroup。未設定時はDialogueViewWindowのCanvasGroupを自動取得")]
        public CanvasGroup fadeCanvasGroup;

        [Header("Portrait Root & Slots (立ち絵スロット)")]
        [Tooltip("立ち絵スロット群の親 Transform または GameObject")]
        public GameObject portraitRoot;

        [Tooltip("立ち絵スロットのリスト（未登録時は子要素の Image から自動収集）")]
        public List<PortraitSlot> portraitSlots = new List<PortraitSlot>();

        public void AutoDetectReferences()
        {
            if (dialogueViewWindow == null)
            {
                dialogueViewWindow = GetComponentInChildren<DialogueViewWindow>(true);
            }

            if (backgroundTargetImage == null && dialogueViewWindow != null)
            {
                backgroundTargetImage = dialogueViewWindow.GetComponent<Image>()
                    ?? dialogueViewWindow.GetComponentInChildren<Image>(true);
            }

            if (fadeCanvasGroup == null)
            {
                // DialogueViewWindow のルートまたは親からCanvasGroupを自動取得
                if (dialogueViewWindow != null)
                {
                    fadeCanvasGroup = dialogueViewWindow.GetComponent<CanvasGroup>()
                        ?? dialogueViewWindow.GetComponentInParent<CanvasGroup>(true);
                }
                // 見つからなければPrefab全体から探す
                if (fadeCanvasGroup == null)
                {
                    fadeCanvasGroup = GetComponentInChildren<CanvasGroup>(true);
                }
            }

            if (portraitRoot == null)
            {
                var pTrans = transform.Find("PortraitRoot") ?? transform.Find("Portraits") ?? transform.Find("Portrait");
                if (pTrans != null) portraitRoot = pTrans.gameObject;
            }

            if (portraitSlots.Count == 0 && portraitRoot != null)
            {
                var images = portraitRoot.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img.gameObject == portraitRoot) continue;
                    string rawName = img.gameObject.name.ToLower();
                    string posId = "center";
                    if (rawName.Contains("left") || rawName == "l") posId = "L";
                    else if (rawName.Contains("right") || rawName == "r") posId = "R";
                    else if (rawName.Contains("center") || rawName.Contains("mid") || rawName == "c") posId = "C";
                    else posId = img.gameObject.name; // 元の名前を保持（例: Icon）

                    portraitSlots.Add(new PortraitSlot
                    {
                        positionID = posId,
                        portraitImage = img,
                        canvasGroup = img.GetComponent<CanvasGroup>(),
                        animator = img.GetComponent<Animator>()
                    });
                }
            }
        }
    }
}
