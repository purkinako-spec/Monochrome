using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Dialogue.Audio
{
    [AddComponentMenu("Dialogue/Core/Dialogue Audio Manager")]
    [DisallowMultipleComponent]
    public class DialogueAudioManager : MonoBehaviour
    {
        private static DialogueAudioManager instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        public static DialogueAudioManager Instance
        {
            get
            {
                if (instance == null || !instance)
                {
                    instance = FindFirstObjectByType<DialogueAudioManager>();
                    if ((instance == null || !instance) && Application.isPlaying)
                    {
                        var go = new GameObject("[DialogueAudioManager]");
                        instance = go.AddComponent<DialogueAudioManager>();
                        if (go.transform.parent == null) DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

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
            if (instance == null || !instance)
            {
                instance = this;
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
                EnsureAudioSources();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void EnsureAudioSources()
        {
            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
            if (seSource == null) seSource = gameObject.AddComponent<AudioSource>();
            if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
            if (typingSource == null) typingSource = gameObject.AddComponent<AudioSource>();

            bgmSource.playOnAwake = false;
            seSource.playOnAwake = false;
            voiceSource.playOnAwake = false;
            typingSource.playOnAwake = false;
        }

        // --- データベースの動的追加・削除機能 ---
        public void AddDatabase(DialogueAudioDatabase db, bool highPriority = false)
        {
            if (db == null || audioDatabases.Contains(db)) return;

            if (highPriority)
            {
                audioDatabases.Insert(0, db);
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
