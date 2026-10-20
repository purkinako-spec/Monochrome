using System;
using UnityEngine;

namespace Runtime.Dialogue.Core
{
    [Serializable]
    public class DialogueLayoutPreset
    {
        [Tooltip("呼び出す際のコマンド名 例: Normal, Narration, NoWindow")]
        public string layoutName;

        [Header("UI Visibility (表示/非表示)")]
        public bool showBackgroundBox = true; // テキスト背景枠
        public bool showNameBox = true;       // 名前枠
        public bool hidePortraits = false;    // このレイアウト時に立ち絵レイヤーを消すか

        [Header("Text RectTransform Options (テキスト表示領域の調整)")]
        [Tooltip("枠の有無に合わせてテキストの表示領域（余白）を変更する場合はtrue")]
        public bool overrideTextRect = false;
        public Vector2 textOffsetMin; // RectTransformの Left, Bottom
        public Vector2 textOffsetMax; // RectTransformの Right, Top
    }
}