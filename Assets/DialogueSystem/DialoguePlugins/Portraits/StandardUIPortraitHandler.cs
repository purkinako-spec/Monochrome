using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

namespace Runtime.Dialogue.Plugins.Portraits
{
    [Serializable]
    public class PortraitSlot
    {
        [Tooltip("コマンドで指定する位置 例: left, center, right")]
        public string positionID;
        [Tooltip("対象となるUIのImageコンポーネント")]
        public Image portraitImage;
        [Tooltip("フェード制御用のCanvasGroup（未設定の場合はImageのColorアルファを使用）")]
        public CanvasGroup canvasGroup;
        [Tooltip("モーション演出用のAnimator（任意）")]
        public Animator animator;

        [HideInInspector] public Coroutine currentRoutine;
        [HideInInspector] public Vector3 defaultPosition;
    }

    public class StandardUIPortraitHandler : MonoBehaviour, IDialoguePortraitHandler
    {
        public int Priority => 0;

        [Header("Character Profiles")]
        public List<CharacterProfile> profiles = new List<CharacterProfile>();

        [Header("UI Slots")]
        public List<PortraitSlot> portraitSlots = new List<PortraitSlot>();

        private void Awake()
        {
            // 初期位置を保存
            foreach (var slot in portraitSlots)
            {
                if (slot.portraitImage != null)
                {
                    slot.defaultPosition = slot.portraitImage.rectTransform.anchoredPosition;
                }
            }
        }

        private void Start()
        {
            if (DialoguePortraitDispatcher.Instance != null)
            {
                DialoguePortraitDispatcher.Instance.RegisterHandler(this);
            }

            foreach (var slot in portraitSlots)
            {
                SetSlotAlpha(slot, 0f);
            }
        }

        public bool TryHandlePortrait(string targetID, string expression, string position, Dictionary<string, string> args, Action onComplete)
        {
            // clearコマンドの処理
            if (targetID.ToLower() == "clear")
            {
                HandleClear(position, args, onComplete);
                return true;
            }

            var profile = profiles.Find(p => p.characterID == targetID);
            if (profile == null) return false;

            var sprite = profile.GetExpression(expression);
            var slot = portraitSlots.Find(s => s.positionID == position);
            if (slot == null || slot.portraitImage == null) return false;

            // コルーチン重なり防止
            if (slot.currentRoutine != null) StopCoroutine(slot.currentRoutine);

            // 引数の解析
            float fadeTime = GetFloatArg(args, "fade", 0f);
            float moveTime = GetFloatArg(args, "moveTime", 0f);
            string motion = GetStringArg(args, "motion", "");

            slot.portraitImage.sprite = sprite;
            slot.portraitImage.gameObject.SetActive(true);

            // 💡 修正ポイント: キャラ固有の AnimatorController を動的割り当て
            if (slot.animator != null)
            {
                if (profile.animatorController != null)
                {
                    slot.animator.runtimeAnimatorController = profile.animatorController;
                }

                if (!string.IsNullOrEmpty(motion)) slot.animator.SetTrigger(motion);
                if (!string.IsNullOrEmpty(expression)) slot.animator.SetTrigger(expression);
            }

            // フェード・移動コルーチンの開始
            slot.currentRoutine = StartCoroutine(ApplyPortraitRoutine(slot, fadeTime, moveTime, onComplete));
            return true;
        }

        private IEnumerator ApplyPortraitRoutine(PortraitSlot slot, float fadeTime, float moveTime, Action onComplete)
        {
            float duration = Mathf.Max(fadeTime, moveTime);
            float timer = 0f;

            float startAlpha = GetSlotAlpha(slot);
            Vector3 startPos = slot.portraitImage.rectTransform.anchoredPosition;
            Vector3 targetPos = slot.defaultPosition;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = Mathf.Clamp01(timer / duration);

                if (fadeTime > 0f)
                {
                    SetSlotAlpha(slot, Mathf.Lerp(startAlpha, 1f, timer / fadeTime));
                }
                else
                {
                    SetSlotAlpha(slot, 1f);
                }

                if (moveTime > 0f)
                {
                    slot.portraitImage.rectTransform.anchoredPosition = Vector3.Lerp(startPos, targetPos, timer / moveTime);
                }

                yield return null;
            }

            SetSlotAlpha(slot, 1f);
            slot.portraitImage.rectTransform.anchoredPosition = targetPos;

            slot.currentRoutine = null;
            onComplete?.Invoke();
        }

        private void HandleClear(string position, Dictionary<string, string> args, Action onComplete)
        {
            float fadeTime = GetFloatArg(args, "fade", 0f);

            List<PortraitSlot> targetSlots = (string.IsNullOrEmpty(position) || position.ToLower() == "all")
                ? portraitSlots
                : portraitSlots.FindAll(s => s.positionID == position);

            int pending = targetSlots.Count;
            if (pending == 0)
            {
                onComplete?.Invoke();
                return;
            }

            foreach (var slot in targetSlots)
            {
                if (slot.currentRoutine != null) StopCoroutine(slot.currentRoutine);

                slot.currentRoutine = StartCoroutine(ClearRoutine(slot, fadeTime, () =>
                {
                    pending--;
                    if (pending <= 0) onComplete?.Invoke();
                }));
            }
        }

        private IEnumerator ClearRoutine(PortraitSlot slot, float fadeTime, Action onComplete)
        {
            if (fadeTime > 0f)
            {
                float timer = 0f;
                float startAlpha = GetSlotAlpha(slot);

                while (timer < fadeTime)
                {
                    timer += Time.deltaTime;
                    SetSlotAlpha(slot, Mathf.Lerp(startAlpha, 0f, timer / fadeTime));
                    yield return null;
                }
            }

            SetSlotAlpha(slot, 0f);
            if (slot.portraitImage != null) slot.portraitImage.gameObject.SetActive(false);

            slot.currentRoutine = null;
            onComplete?.Invoke();
        }

        public void ForceCompletePortrait(string targetID, string expression, string position, Dictionary<string, string> args)
        {
            StopAllCoroutines();
            if (targetID.ToLower() == "clear")
            {
                foreach (var s in portraitSlots)
                {
                    SetSlotAlpha(s, 0f);
                    if (s.portraitImage != null) s.portraitImage.gameObject.SetActive(false);
                }
                return;
            }

            var profile = profiles.Find(p => p.characterID == targetID);
            var slot = portraitSlots.Find(s => s.positionID == position);
            if (profile != null && slot != null && slot.portraitImage != null)
            {
                // スキップ時も AnimatorController を動的セット
                if (slot.animator != null && profile.animatorController != null)
                {
                    slot.animator.runtimeAnimatorController = profile.animatorController;
                }

                slot.portraitImage.sprite = profile.GetExpression(expression);
                SetSlotAlpha(slot, 1f);
                slot.portraitImage.gameObject.SetActive(true);
            }
        }

        private void SetSlotAlpha(PortraitSlot slot, float alpha)
        {
            if (slot.canvasGroup != null)
            {
                slot.canvasGroup.alpha = alpha;
            }
            else if (slot.portraitImage != null)
            {
                Color c = slot.portraitImage.color;
                c.a = alpha;
                slot.portraitImage.color = c;
            }
        }

        private float GetSlotAlpha(PortraitSlot slot)
        {
            if (slot.canvasGroup != null) return slot.canvasGroup.alpha;
            if (slot.portraitImage != null) return slot.portraitImage.color.a;
            return 0f;
        }

        private float GetFloatArg(Dictionary<string, string> args, string key, float defaultValue)
        {
            if (args != null && args.TryGetValue(key, out string val) && float.TryParse(val, out float res))
                return res;
            return defaultValue;
        }

        private string GetStringArg(Dictionary<string, string> args, string key, string defaultValue)
        {
            if (args != null && args.TryGetValue(key, out string val))
                return val;
            return defaultValue;
        }
    }
}