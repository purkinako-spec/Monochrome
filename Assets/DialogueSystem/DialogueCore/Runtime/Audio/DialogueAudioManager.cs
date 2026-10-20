using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Dialogue.Audio
{
    public class DialogueAudioManager : MonoBehaviour
    {
        public static DialogueAudioManager Instance { get; private set; }

        [Header("Audio Databases")]
        [Tooltip("使用するオーディオデータベースのリスト（先頭の要素ほど優先的に検索されます）")]
        [SerializeField] private List<DialogueAudioDatabase> audioDatabases = new List<DialogueAudioDatabase>();

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource seSource;
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private AudioSource typingSource;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // --- データベースの動的追加・削除機能 ---
        /// <summary>
        /// データベースを追加します（指定があれば先頭に挿入して優先検索に設定可能）
        /// </summary>
        public void AddDatabase(DialogueAudioDatabase db, bool highPriority = false)
        {
            if (db == null || audioDatabases.Contains(db)) return;

            if (highPriority)
            {
                audioDatabases.Insert(0, db); // シーン固有データなどを優先したい場合は先頭に挿入
            }
            else
            {
                audioDatabases.Add(db);
            }
        }

        public void RemoveDatabase(DialogueAudioDatabase db)
        {
            if (db != null && audioDatabases.Contains(db))
            {
                audioDatabases.Remove(db);
            }
        }

        public void ClearDatabases()
        {
            audioDatabases.Clear();
        }

        // --- 音声検索ロジック ---
        private AudioClip FindAudioClip(string key, AudioType type)
        {
            foreach (var db in audioDatabases)
            {
                if (db == null) continue;
                var clip = db.GetClip(key, type);
                if (clip != null) return clip;
            }

            Debug.LogWarning($"[DialogueAudioManager] キー '{key}' (種別: {type}) のAudioClipが見つかりませんでした。");
            return null;
        }

        // --- 再生処理 ---
        public void PlayBGM(string key, bool loop = true)
        {
            var clip = FindAudioClip(key, AudioType.BGM);
            if (clip == null || bgmSource == null) return;

            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.Play();
        }

        public void StopBGM() => bgmSource?.Stop();

        public void PlaySE(string key)
        {
            var clip = FindAudioClip(key, AudioType.SE);
            if (clip != null && seSource != null)
                seSource.PlayOneShot(clip);
        }

        public void PlayVoice(string key, Action onComplete = null)
        {
            var clip = FindAudioClip(key, AudioType.Voice);
            if (clip != null && voiceSource != null)
            {
                voiceSource.clip = clip;
                voiceSource.Play();
            }
        }

        public bool IsVoicePlaying => voiceSource != null && voiceSource.isPlaying;

        public void PlayTypingSound(string key = null, float pitchMin = 0.95f, float pitchMax = 1.05f)
        {
            if (typingSource == null) return;
            if (IsVoicePlaying) return;

            AudioClip clipToPlay = null;
            if (!string.IsNullOrEmpty(key))
            {
                clipToPlay = FindAudioClip(key, AudioType.Typing);
            }

            if (clipToPlay == null) clipToPlay = typingSource.clip;
            if (clipToPlay == null) return;

            typingSource.pitch = UnityEngine.Random.Range(pitchMin, pitchMax);
            typingSource.PlayOneShot(clipToPlay);
        }
    }
}