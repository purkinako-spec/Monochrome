using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Dialogue.Core
{
    public class DialogueLogManager : MonoBehaviour
    {
        public static DialogueLogManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField, Tooltip("保存するログの最大件数")]
        private int maxLogCount = 100;

        private readonly List<DialogueLogData> logList = new List<DialogueLogData>();

        /// <summary>ログが追加された時に発火するイベント（UI側の更新通知用）</summary>
        public event Action<DialogueLogData> OnLogAdded;
        /// <summary>ログがクリアされた時に発火するイベント</summary>
        public event Action OnLogCleared;

        public IReadOnlyList<DialogueLogData> Logs => logList;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 新しい会話ログを追加します
        /// </summary>
        public void AddLog(string speakerName, string text, string voiceID = "")
        {
            if (string.IsNullOrEmpty(text)) return;

            var newLog = new DialogueLogData(speakerName, text, voiceID);
            logList.Add(newLog);

            // 最大件数を超えた場合は古いものから削除
            if (logList.Count > maxLogCount)
            {
                logList.RemoveAt(0);
            }

            OnLogAdded?.Invoke(newLog);
        }

        /// <summary>
        /// 蓄積されたログを全て消去します
        /// </summary>
        public void ClearLogs()
        {
            logList.Clear();
            OnLogCleared?.Invoke();
        }
    }
}