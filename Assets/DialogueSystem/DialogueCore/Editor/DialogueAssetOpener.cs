using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using Runtime.Dialogue.Core;

namespace DialogueSystem.Editor
{
    /// <summary>
    /// Project ウィンドウで DialogueContainer アセットがダブルクリックされた時に
    /// 自動的に DialogueEditorWindow を開き、対象コンテナをロードするアセットオープナー
    /// </summary>
    public static class DialogueAssetOpener
    {
        [OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var asset = EditorUtility.EntityIdToObject(instanceID) as DialogueContainer;
            if (asset != null)
            {
                DialogueEditorWindow.OpenAndLoadContainer(asset);
                return true; // 処理済みとしてUnity標準処理を消費
            }
            return false;
        }
    }
}
