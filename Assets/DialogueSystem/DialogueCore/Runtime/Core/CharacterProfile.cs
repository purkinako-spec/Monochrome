using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Dialogue.Core
{
    [Serializable]
    public class ExpressionData
    {
        public string expressionID;
        public Sprite faceSprite;
    }

    [CreateAssetMenu(fileName = "NewCharacterProfile", menuName = "Dialogue/Character Profile")]
    public class CharacterProfile : ScriptableObject
    {
        public string characterID;
        public string displayName;

        [Tooltip("キャラクター固有のAnimatorController（モーション再生時に動的割り当て）")]
        public RuntimeAnimatorController animatorController;

        [Tooltip("このキャラクターが喋る時の標準フォントスタイル（例: default, horror, robot等。空欄の場合は全体標準フォント）")]
        public string defaultFontStyle = "default";

        public List<ExpressionData> expressions = new List<ExpressionData>();

        public Sprite GetExpression(string id)
        {
            var data = expressions.Find(e => e.expressionID == id);
            return data?.faceSprite;
        }
    }
}
