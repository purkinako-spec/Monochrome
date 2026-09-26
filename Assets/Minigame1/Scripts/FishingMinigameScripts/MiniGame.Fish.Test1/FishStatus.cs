using System;
using UnityEngine;

namespace MiniGame.Fish.Test1
{

    [CreateAssetMenu(fileName = "NewFishStatus", menuName = "Fish/FishStatus")]
    public class FishStatus : ScriptableObject
    {
        public string fishName;
        public int level;
        public float rate;
        public string profile;
        public Sprite fishImage;
        public float fishSpeed;
    }
}