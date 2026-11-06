using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Dialogue.Core
{
    /// <summary>
    /// SEデータベース（DialogueAudioDatabase）と同様に、
    /// 複数のキャラクター立ち絵プロファイルを1つにまとめて管理するデータベース。
    /// </summary>
    [CreateAssetMenu(fileName = "DialoguePortraitDatabase", menuName = "Dialogue/Portrait Database")]
    public class DialoguePortraitDatabase : ScriptableObject
    {
        [Tooltip("このデータベースに含めるキャラクタープロファイルのリスト")]
        [SerializeField] private List<CharacterProfile> profiles = new List<CharacterProfile>();

        public IReadOnlyList<CharacterProfile> Profiles => profiles;

        /// <summary>
        /// キャラクターIDからプロファイルを取得します（大文字小文字無視）
        /// </summary>
        public CharacterProfile GetProfile(string characterID)
        {
            if (string.IsNullOrEmpty(characterID)) return null;
            return profiles.Find(p => p != null && p.characterID.Equals(characterID, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// キャラクターIDと表情IDからスプライトを取得します
        /// </summary>
        public Sprite GetExpression(string characterID, string expressionID)
        {
            var profile = GetProfile(characterID);
            return profile != null ? profile.GetExpression(expressionID) : null;
        }

        /// <summary>
        /// データベースにプロファイルを追加します
        /// </summary>
        public void AddProfile(CharacterProfile profile)
        {
            if (profile != null && !profiles.Contains(profile))
            {
                profiles.Add(profile);
            }
        }

        /// <summary>
        /// データベースからプロファイルを削除します
        /// </summary>
        public void RemoveProfile(CharacterProfile profile)
        {
            if (profile != null && profiles.Contains(profile))
            {
                profiles.Remove(profile);
            }
        }
    }
}
