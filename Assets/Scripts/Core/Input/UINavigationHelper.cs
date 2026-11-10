using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Monochrome.Core.Input
{
    /// <summary>
    /// ゲームパッド・キーボード・タッチ操作における UI フォーカスの制御を支援するヘルパー。
    /// - 選択肢やメニューを開いた際の初期ボタン自動選択
    /// - 画面外クリック等でフォーカスが外れた場合の復元
    /// - マウス/タッチ操作とゲームパッド操作のシームレスな切り替え
    /// </summary>
    public static class UINavigationHelper
    {
        private static GameObject lastSelectedObject;

        /// <summary>
        /// 指定した UI 要素（ボタン等）に初期フォーカスを合わせます。
        /// ゲームパッドやキーボード操作時に選択肢が表示された直後に呼び出します。
        /// </summary>
        public static void Focus(GameObject target)
        {
            if (target == null) return;

            var es = EventSystem.current;
            if (es == null) return;

            // タッチデバイスの場合はフォーカス枠を出さない
            var device = InputDeviceManager.Instance.CurrentDeviceType;
            if (device == InputDeviceType.Touchscreen)
            {
                es.SetSelectedGameObject(null);
                return;
            }

            lastSelectedObject = target;
            es.SetSelectedGameObject(target);

            // Selectable コンポーネントがある場合は OnSelect を確実に実行
            var selectable = target.GetComponent<Selectable>();
            selectable?.Select();
        }

        /// <summary>
        /// フォーカスが何もない状態（画面外クリック等）になった場合、直前のオブジェクトを再選択します。
        /// ゲームパッド操作時のスタック（操作不能）を防止します。
        /// </summary>
        public static void EnsureSelection()
        {
            var es = EventSystem.current;
            if (es == null) return;

            var device = InputDeviceManager.Instance.CurrentDeviceType;
            if (device == InputDeviceType.Touchscreen) return;

            if (es.currentSelectedGameObject == null && lastSelectedObject != null && lastSelectedObject.activeInHierarchy)
            {
                es.SetSelectedGameObject(lastSelectedObject);
            }
        }
    }
}
