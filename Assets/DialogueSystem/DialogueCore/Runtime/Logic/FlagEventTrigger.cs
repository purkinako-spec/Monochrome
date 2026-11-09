using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Logic
{
    /// <summary>
    /// フラグの変更を検知し、条件を満たしたときに UnityEvent を発火するコンポーネント。
    /// ドアの解錠、演出トリガー、NPCのアクティブ切替、SE再生などをインスペクタから設定できます。
    /// </summary>
    [AddComponentMenu("Dialogue System/Flag Event Trigger")]
    public class FlagEventTrigger : MonoBehaviour
    {
        public enum TriggerMode
        {
            SingleFlag,
            MultipleConditions
        }

        [Header("Mode")]
        [SerializeField] private TriggerMode mode = TriggerMode.SingleFlag;

        [Header("Single Flag Condition")]
        [SerializeField] private string flagKey = "";
        [SerializeField] private ConditionOperator conditionOperator = ConditionOperator.Equal;
        [SerializeField] private int targetValue = 1;

        [Header("Multiple Conditions (AND)")]
        [SerializeField] private List<FlagCondition> conditions = new List<FlagCondition>();

        [Header("Trigger Options")]
        [Tooltip("Start() 実行時に条件を満たしている場合にも発火するか")]
        [SerializeField] private bool checkOnStart = true;

        [Tooltip("一度発火したら二度と発火しないか")]
        [SerializeField] private bool triggerOnce = false;

        [Header("Events")]
        [Tooltip("条件を満たした（または成立状態に遷移した）ときに呼ばれるイベント")]
        [SerializeField] private UnityEvent onConditionMet = new UnityEvent();

        [Tooltip("条件を満たさなくなったときに呼ばれるイベント")]
        [SerializeField] private UnityEvent onConditionUnmet = new UnityEvent();

        [Tooltip("フラグ値が変化するたびに最新の値を引数として呼ばれるイベント（SingleFlag時のみ）")]
        [SerializeField] private UnityEvent<int> onValueChanged = new UnityEvent<int>();

        private bool hasTriggered = false;
        private bool wasSatisfied = false;

        private void OnEnable()
        {
            if (FlagManager.Instance != null)
            {
                FlagManager.Instance.OnFlagChanged += HandleFlagChanged;
            }
        }

        private void OnDisable()
        {
            if (FlagManager.Instance != null)
            {
                FlagManager.Instance.OnFlagChanged -= HandleFlagChanged;
            }
        }

        private void Start()
        {
            if (checkOnStart)
            {
                EvaluateAndTrigger(isInitialCheck: true);
            }
        }

        private void HandleFlagChanged(string key, int newValue)
        {
            if (mode == TriggerMode.SingleFlag)
            {
                if (key == flagKey)
                {
                    onValueChanged?.Invoke(newValue);
                    EvaluateAndTrigger(isInitialCheck: false);
                }
            }
            else
            {
                // 複数条件に含まれているキーか判定
                bool relevant = false;
                if (conditions != null)
                {
                    for (int i = 0; i < conditions.Count; i++)
                    {
                        if (conditions[i].key == key)
                        {
                            relevant = true;
                            break;
                        }
                    }
                }

                if (relevant)
                {
                    EvaluateAndTrigger(isInitialCheck: false);
                }
            }
        }

        /// <summary>
        /// 条件を評価し、イベントを発火します。手動で呼び出すことも可能です。
        /// </summary>
        public void EvaluateAndTrigger(bool isInitialCheck = false)
        {
            if (triggerOnce && hasTriggered) return;

            var manager = FlagManager.Instance;
            if (manager == null) return;

            bool isSatisfied = false;
            if (mode == TriggerMode.SingleFlag)
            {
                isSatisfied = manager.EvaluateCondition(flagKey, conditionOperator, targetValue);
            }
            else
            {
                isSatisfied = manager.EvaluateAll(conditions);
            }

            if (isSatisfied)
            {
                if (isInitialCheck || !wasSatisfied)
                {
                    hasTriggered = true;
                    wasSatisfied = true;
                    onConditionMet?.Invoke();
                }
            }
            else
            {
                if (wasSatisfied)
                {
                    wasSatisfied = false;
                    onConditionUnmet?.Invoke();
                }
            }
        }

        /// <summary>
        /// 外部からトリガー状態をリセットして再発火を可能にします。
        /// </summary>
        public void ResetTrigger()
        {
            hasTriggered = false;
            wasSatisfied = false;
        }
    }
}
