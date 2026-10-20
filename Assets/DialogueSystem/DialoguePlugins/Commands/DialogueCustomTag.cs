using UnityEngine;

namespace Runtime.Dialogue.Plugins.Commands
{
    public class DialogueCustomTag : MonoBehaviour
    {
        [SerializeField] private string tagName;
        public string TagName => tagName;
    }
}