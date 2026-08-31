using UnityEngine;

namespace MiniGame.Fish
{
    public class FishSpawner : MonoBehaviour
    {
        public static FishSpawner Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        void  Spawn()
        {

        }
    }
}