using UnityEngine;

namespace Runtime.Dialogue.Audio
{
    public class DialogueTypewriterAudio : MonoBehaviour
    {
        [Header("タイピング音設定")]
        [SerializeField] private string defaultTypingKey = "default_typing"; // データベースに登録したKey名
        [SerializeField] private int playIntervalCharacters = 2; // N文字ごとに1回再生
        [SerializeField] private bool useRandomPitch = true;

        private int characterCounter = 0;

        public void ResetCounter() => characterCounter = 0;

        /// <summary>
        /// テキスト送りの1文字表示ごとにUI/Typewriter側から呼び出す
        /// </summary>
        /// <param name="customTypingKey">キャラ固有のタイプ音キー（指定がなければデフォルト音）</param>
        public void OnCharacterTyped(string customTypingKey = null)
        {
            characterCounter++;
            if (characterCounter % playIntervalCharacters != 0) return;

            // カスタムキーがあればそれを使い、無ければデフォルトのキーを使用
            string keyToPlay = !string.IsNullOrEmpty(customTypingKey) ? customTypingKey : defaultTypingKey;

            if (useRandomPitch)
            {
                DialogueAudioManager.Instance?.PlayTypingSound(keyToPlay, 0.9f, 1.1f);
            }
            else
            {
                DialogueAudioManager.Instance?.PlayTypingSound(keyToPlay, 1.0f, 1.0f);
            }
        }
    }
}