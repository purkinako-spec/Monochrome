using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using Monochrome.Core.Dialogue;

namespace Runtime.Dialogue.Editor
{
    public static class DialogueLauncherUIPrefabCreator
    {
        [MenuItem("Tools/Monochrome/Create DialogueLauncherUI Prefab")]
        [InitializeOnLoadMethod]
        public static void CreatePrefab()
        {
            string folderPath = "Assets/Prefabs/UI";
            string prefabPath = folderPath + "/DialogueLauncherUI.prefab";

            if (File.Exists(prefabPath))
            {
                return;
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            // 1. Root GameObject
            GameObject root = new GameObject("DialogueLauncherUI");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            var launcher = root.AddComponent<DialogueLauncherUI>();

            // 2. LauncherButton
            GameObject btnObj = new GameObject("LauncherButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(CanvasGroup));
            btnObj.transform.SetParent(root.transform, false);

            var btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0f);
            btnRt.anchorMax = new Vector2(1f, 0f);
            btnRt.pivot = new Vector2(1f, 0f);
            btnRt.anchoredPosition = new Vector2(-40f, 40f);
            btnRt.sizeDelta = new Vector2(180f, 54f);

            var btnImg = btnObj.GetComponent<Image>();
            btnImg.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);

            var launcherBtn = btnObj.GetComponent<Button>();
            var launcherCg = btnObj.GetComponent<CanvasGroup>();

            GameObject textObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            var launcherTmp = textObj.GetComponent<TextMeshProUGUI>();
            launcherTmp.text = "[F] 会話";
            launcherTmp.fontSize = 24;
            launcherTmp.alignment = TextAlignmentOptions.Center;
            launcherTmp.color = Color.white;

            // 3. SelectionModalPanel
            GameObject modalObj = new GameObject("SelectionModalPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            modalObj.transform.SetParent(root.transform, false);
            var modalRt = modalObj.GetComponent<RectTransform>();
            modalRt.anchorMin = new Vector2(0.5f, 0.5f);
            modalRt.anchorMax = new Vector2(0.5f, 0.5f);
            modalRt.pivot = new Vector2(0.5f, 0.5f);
            modalRt.anchoredPosition = Vector2.zero;
            modalRt.sizeDelta = new Vector2(440f, 320f);

            var modalImg = modalObj.GetComponent<Image>();
            modalImg.color = new Color(0.08f, 0.08f, 0.1f, 0.96f);

            // Header
            GameObject headerObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            headerObj.transform.SetParent(modalObj.transform, false);
            var hrt = headerObj.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, -15f);
            hrt.sizeDelta = new Vector2(0f, 40f);
            var htmp = headerObj.GetComponent<TextMeshProUGUI>();
            htmp.text = "会話を選択";
            htmp.fontSize = 26;
            htmp.alignment = TextAlignmentOptions.Center;
            htmp.color = Color.white;

            // CloseButton
            GameObject closeObj = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeObj.transform.SetParent(modalObj.transform, false);
            var crt = closeObj.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-15f, -15f);
            crt.sizeDelta = new Vector2(36f, 36f);
            closeObj.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f, 0.8f);
            var closeBtn = closeObj.GetComponent<Button>();

            GameObject closeTextObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            closeTextObj.transform.SetParent(closeObj.transform, false);
            var ctrt = closeTextObj.GetComponent<RectTransform>();
            ctrt.anchorMin = Vector2.zero;
            ctrt.anchorMax = Vector2.one;
            ctrt.sizeDelta = Vector2.zero;
            var ctmp = closeTextObj.GetComponent<TextMeshProUGUI>();
            ctmp.text = "✕";
            ctmp.fontSize = 20;
            ctmp.alignment = TextAlignmentOptions.Center;
            ctmp.color = Color.white;

            // Content (スロットボタンを並べる親)
            GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            contentObj.transform.SetParent(modalObj.transform, false);
            var contRt = contentObj.GetComponent<RectTransform>();
            contRt.anchorMin = new Vector2(0.05f, 0.05f);
            contRt.anchorMax = new Vector2(0.95f, 0.82f);
            contRt.sizeDelta = Vector2.zero;

            var vlg = contentObj.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(35, 15, 10, 10);

            // スロットボタンを初期3個配置（配置された数でスクロールまでの表示数を制御）
            var slotBtnList = new List<Button>();
            var choiceBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DialogueSystem/Data/Prefabs/ChoiceButton.prefab");

            for (int i = 0; i < 3; i++)
            {
                GameObject slotBtnObj = null;
                if (choiceBtnPrefab != null)
                {
                    slotBtnObj = (GameObject)PrefabUtility.InstantiatePrefab(choiceBtnPrefab, contentObj.transform);
                    slotBtnObj.name = $"SlotButton_{i + 1}";
                }
                else
                {
                    slotBtnObj = new GameObject($"SlotButton_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                    slotBtnObj.transform.SetParent(contentObj.transform, false);

                    var img = slotBtnObj.GetComponent<Image>();
                    img.color = new Color(0.2f, 0.2f, 0.22f, 0.95f);

                    var rt = slotBtnObj.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(360f, 48f);

                    var txt = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    txt.transform.SetParent(slotBtnObj.transform, false);
                    var trt = txt.GetComponent<RectTransform>();
                    trt.anchorMin = Vector2.zero;
                    trt.anchorMax = Vector2.one;
                    trt.sizeDelta = Vector2.zero;
                    trt.offsetMin = new Vector2(35f, 0f);
                    trt.offsetMax = new Vector2(-15f, 0f);

                    var t = txt.GetComponent<TextMeshProUGUI>();
                    t.fontSize = 22;
                    t.color = Color.white;
                    t.alignment = TextAlignmentOptions.MidlineLeft;
                }

                slotBtnObj.transform.localScale = Vector3.one;
                slotBtnList.Add(slotBtnObj.GetComponent<Button>());
            }

            // Cursor
            GameObject cursor = new GameObject("Cursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            cursor.transform.SetParent(modalObj.transform, false);
            var curRt = cursor.GetComponent<RectTransform>();
            curRt.sizeDelta = new Vector2(30f, 30f);
            var curTmp = cursor.GetComponent<TextMeshProUGUI>();
            curTmp.text = "▶";
            curTmp.fontSize = 24;
            curTmp.alignment = TextAlignmentOptions.Center;
            curTmp.color = Color.yellow;
            curTmp.raycastTarget = false;
            cursor.SetActive(false);

            modalObj.SetActive(false); // 初期状態は非表示

            // References
            var serializedLauncher = new SerializedObject(launcher);
            serializedLauncher.FindProperty("launcherButton").objectReferenceValue = launcherBtn;
            serializedLauncher.FindProperty("launcherButtonText").objectReferenceValue = launcherTmp;
            serializedLauncher.FindProperty("launcherButtonCanvasGroup").objectReferenceValue = launcherCg;
            serializedLauncher.FindProperty("selectionModalPanel").objectReferenceValue = modalObj;
            serializedLauncher.FindProperty("contentContainer").objectReferenceValue = contentObj.transform;
            serializedLauncher.FindProperty("closeButton").objectReferenceValue = closeBtn;
            serializedLauncher.FindProperty("cursorObject").objectReferenceValue = cursor;

            var dialoguePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DialogueSystem/Prefab/Dialogue.prefab");
            if (dialoguePrefab != null)
            {
                serializedLauncher.FindProperty("dialogueSystemPrefab").objectReferenceValue = dialoguePrefab;
            }

            if (choiceBtnPrefab != null)
            {
                serializedLauncher.FindProperty("choiceButtonPrefab").objectReferenceValue = choiceBtnPrefab;
            }

            // Slot buttons property
            var slotProp = serializedLauncher.FindProperty("slotButtons");
            slotProp.ClearArray();
            for (int i = 0; i < slotBtnList.Count; i++)
            {
                slotProp.InsertArrayElementAtIndex(i);
                slotProp.GetArrayElementAtIndex(i).objectReferenceValue = slotBtnList[i];
            }

            // Sample entries
            var entriesProp = serializedLauncher.FindProperty("entries");
            entriesProp.ClearArray();

            entriesProp.InsertArrayElementAtIndex(0);
            var elem0 = entriesProp.GetArrayElementAtIndex(0);
            elem0.FindPropertyRelative("id").stringValue = "murabito";
            elem0.FindPropertyRelative("defaultTitle").stringValue = "村人と話す";
            elem0.FindPropertyRelative("dialogueContainerAddress").stringValue = "murabito";
            elem0.FindPropertyRelative("isEnabled").boolValue = true;

            entriesProp.InsertArrayElementAtIndex(1);
            var elem1 = entriesProp.GetArrayElementAtIndex(1);
            elem1.FindPropertyRelative("id").stringValue = "fishing";
            elem1.FindPropertyRelative("defaultTitle").stringValue = "釣りの会話";
            elem1.FindPropertyRelative("dialogueContainerAddress").stringValue = "FishingMiniGameDialogueContainer";
            elem1.FindPropertyRelative("isEnabled").boolValue = true;

            serializedLauncher.ApplyModifiedProperties();

            // Save Prefab
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=lime>[DialogueLauncherUI]</color> Prefab を正常に作成しました: <b>{prefabPath}</b> (スロットボタン数: {slotBtnList.Count})");
        }
    }
}
