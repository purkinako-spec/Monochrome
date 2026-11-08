using System;
using UnityEngine;

namespace Runtime.Dialogue.Audio
{
    /// <summary>
    /// 文字列フィールドに DialogueAudioDatabase に登録されたキー一覧をドロップダウン表示する属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class DialogueAudioKeyAttribute : PropertyAttribute
    {
        public AudioType? AudioTypeFilter { get; }

        public DialogueAudioKeyAttribute()
        {
            AudioTypeFilter = null;
        }

        public DialogueAudioKeyAttribute(AudioType filter)
        {
            AudioTypeFilter = filter;
        }
    }
}
