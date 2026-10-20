using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Dialogue.Audio
{
    public enum AudioType
    {
        BGM,
        SE,
        Voice,
        Typing
    }

    [System.Serializable]
    public struct AudioData
    {
        public string key;
        public AudioType type;
        public AudioClip clip;
    }

    [CreateAssetMenu(fileName = "DialogueAudioDatabase", menuName = "Dialogue/Audio Database")]
    public class DialogueAudioDatabase : ScriptableObject
    {
        [SerializeField] private List<AudioData> audioDataList = new List<AudioData>();

        /// <summary>
        /// キーと種別からオーディオクリップを検索
        /// </summary>
        public AudioClip GetClip(string key, AudioType type)
        {
            var data = audioDataList.Find(x => x.type == type && x.key.Equals(key, StringComparison.OrdinalIgnoreCase));
            return data.clip;
        }

        /// <summary>
        /// キーのみで検索
        /// </summary>
        public AudioClip GetClip(string key)
        {
            var data = audioDataList.Find(x => x.key.Equals(key, StringComparison.OrdinalIgnoreCase));
            return data.clip;
        }
    }
}