using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

namespace Runtime.Dialogue.Commands
{
    /// <summary>
    /// [shake:target=window|canvas|both,magnitude=5,time=0.5,mode=pos|rot|both] を処理するコマンドハンドラー
    /// InspectorでCanvasGroupのリストを直接指定可能
    /// </summary>
    [HandlerInfo(
        description: "ダイアログウィンドウまたはCanvasGroupを指定した大きさ・時間で揺らします。対象、大きさ、時間、モードを指定可能です。CanvasGroupはInspectorのリストで直接指定できます（未指定時は自動検索）。",
        usage: "[shake:target=window|canvas|both,magnitude=5,time=0.5,mode=pos|rot|both]\n  target: 揺らす対象 (window: ダイアログウィンドウ, canvas: Inspector指定CanvasGroup, both: 両方)\n  magnitude: 揺れの大きさ（例: 5）\n  time: 揺れの継続時間（秒）（例: 0.5）\n  mode: 揺れの種類 (pos: 位置のみ, rot: 回転のみ, both: 両方)\n例: [shake:target=canvas,magnitude=10,time=0.3,mode=pos]"
    )]
    public class ShakeCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "shake";

        [Header("CanvasGroup Targets (for target=canvas)")]
        [Tooltip("揺らすCanvasGroupのリスト。空の場合は自動検索（自身→親→子）を行います。")]
        [SerializeField] private List<CanvasGroup> _canvasGroups = new List<CanvasGroup>();

        // 複数ターゲット同時再生用
        private List<Coroutine> _shakeCoroutines = new List<Coroutine>();
        // 元の状態復帰用
        private struct ShakeState
        {
            public Transform Transform;
            public RectTransform RectTransform;
            public Vector3 OriginalLocalPos;
            public Quaternion OriginalLocalRot;
            public Vector2 OriginalAnchoredPos;
            public bool UseRectTransform;
        }
        private List<ShakeState> _activeStates = new List<ShakeState>();

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
                DialogueEventDispatcher.Instance.RegisterHandler(this);
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            string targetStr = command.GetString("target", "window").ToLower();
            float magnitude = command.GetFloat("magnitude", 5f);
            float time = command.GetFloat("time", 0.5f);
            string modeStr = command.GetString("mode", "pos").ToLower();

            var view = UnityEngine.Object.FindObjectOfType<DialogueViewWindow>();
            if (view == null)
            {
                Debug.LogWarning("[ShakeCommandHandler] DialogueViewWindow がシーンに見つかりません。");
                onComplete?.Invoke();
                return;
            }

            _activeStates.Clear();
            
            // ターゲット収集
            if (targetStr == "window" || targetStr == "both")
            {
                Transform target = view.windowRoot != null ? view.windowRoot.transform : view.transform;
                AddTarget(target);
            }
            if (targetStr == "canvas" || targetStr == "both")
            {
                // Inspectorで指定されたCanvasGroupリストを使用
                bool foundAny = false;
                foreach (var cg in _canvasGroups)
                {
                    if (cg != null)
                    {
                        AddTarget(cg.transform);
                        foundAny = true;
                    }
                }

                // リストが空または有効なものがなかった場合、従来の自動検索をフォールバックとして実行
                if (!foundAny)
                {
                    CanvasGroup cg = view.GetComponent<CanvasGroup>();
                    if (cg == null) cg = view.GetComponentInParent<CanvasGroup>();
                    if (cg == null) cg = view.GetComponentInChildren<CanvasGroup>();

                    if (cg != null)
                    {
                        AddTarget(cg.transform);
                        foundAny = true;
                    }
                }

                if (!foundAny)
                {
                    Debug.LogWarning("[ShakeCommandHandler] 有効なCanvasGroupが見つかりません (target=canvas)。Inspectorで指定するか、DialogueViewWindowにCanvasGroupをアタッチしてください。");
                }
            }

            if (_activeStates.Count == 0)
            {
                Debug.LogWarning("[ShakeCommandHandler] 有効な揺れ対象がありません。");
                onComplete?.Invoke();
                return;
            }

            // 既存のコルーチン停止
            foreach (var c in _shakeCoroutines) if (c != null) StopCoroutine(c);
            _shakeCoroutines.Clear();

            // 全ターゲットでコルーチン開始
            // 完了通知は最後の1回だけ行う
            int completedCount = 0;
            Action onSingleComplete = () => {
                completedCount++;
                if (completedCount >= _activeStates.Count)
                {
                    _shakeCoroutines.Clear();
                    onComplete?.Invoke();
                }
            };

            foreach (var state in _activeStates)
            {
                var coroutine = StartCoroutine(ShakeRoutine(state, magnitude, time, modeStr, onSingleComplete));
                _shakeCoroutines.Add(coroutine);
            }
        }

        // ターゲット登録ヘルパー
        private void AddTarget(Transform trans)
        {
            var rect = trans as RectTransform;
            var state = new ShakeState
            {
                Transform = trans,
                RectTransform = rect,
                UseRectTransform = (rect != null), // RectTransformなら true
                OriginalLocalPos = trans.localPosition,
                OriginalLocalRot = trans.localRotation,
                OriginalAnchoredPos = rect != null ? rect.anchoredPosition : Vector2.zero
            };
            _activeStates.Add(state);
        }

        public void ForceComplete(DialogueCommand command)
        {
            foreach (var c in _shakeCoroutines) if (c != null) StopCoroutine(c);
            _shakeCoroutines.Clear();

            // 全ターゲットを元に戻す
            foreach (var state in _activeStates)
            {
                ResetTransform(state);
            }
            _activeStates.Clear();
        }

        private void ResetTransform(ShakeState state)
        {
            if (state.Transform == null) return;

            if (state.UseRectTransform && state.RectTransform != null)
            {
                state.RectTransform.anchoredPosition = state.OriginalAnchoredPos;
                state.RectTransform.localRotation = state.OriginalLocalRot;
            }
            else
            {
                state.Transform.localPosition = state.OriginalLocalPos;
                state.Transform.localRotation = state.OriginalLocalRot;
            }
        }

        private IEnumerator ShakeRoutine(ShakeState state, float magnitude, float duration, string mode, Action onComplete)
        {
            float elapsed = 0f;
            bool doPos = mode.Contains("pos") || mode == "both";
            bool doRot = mode.Contains("rot") || mode == "both";

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float percent = elapsed / duration;
                float damper = 1f - percent; // 減衰

                float offsetX = 0f, offsetY = 0f, rotZ = 0f;

                if (doPos)
                {
                    offsetX = UnityEngine.Random.Range(-1f, 1f) * magnitude * damper;
                    offsetY = UnityEngine.Random.Range(-1f, 1f) * magnitude * damper;
                }
                if (doRot)
                {
                    rotZ = UnityEngine.Random.Range(-1f, 1f) * magnitude * damper;
                }

                if (state.UseRectTransform && state.RectTransform != null)
                {
                    // UI (Overlay Canvas対応) は anchoredPosition で揺らす
                    state.RectTransform.anchoredPosition = state.OriginalAnchoredPos + new Vector2(offsetX, offsetY);
                    state.RectTransform.localRotation = state.OriginalLocalRot * Quaternion.Euler(0f, 0f, rotZ);
                }
                else
                {
                    // 3DオブジェクトやWorld Space Canvas等は localPosition
                    state.Transform.localPosition = state.OriginalLocalPos + new Vector3(offsetX, offsetY, 0f);
                    state.Transform.localRotation = state.OriginalLocalRot * Quaternion.Euler(0f, 0f, rotZ);
                }

                yield return null;
            }

            // 確実に元に戻す
            ResetTransform(state);
            onComplete?.Invoke();
        }
    }
}
