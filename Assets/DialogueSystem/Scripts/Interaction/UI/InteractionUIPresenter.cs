using UnityEngine;
using TMPro; // TextMeshPro 使用想定
using Runtime.Dialogue.Interaction;

namespace Runtime.Dialogue.Interaction.UI
{
    /// <summary>
    /// PlayerInteractor (Script 1) のイベントを購読し、プロンプト表示を行う
    /// </summary>
    public class InteractionUIPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private GameObject _promptRoot;
        [SerializeField] private TextMeshProUGUI _promptText;
        [SerializeField] private string _keyDisplayName = "E"; // 将来的に InputSystem から動的取得推奨

        private void Awake()
        {
            if (_interactor == null) _interactor = GetComponentInParent<PlayerInteractor>();
            if (_promptRoot != null) _promptRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (_interactor != null)
                _interactor.OnTargetChanged += HandleTargetChanged;
        }

        private void OnDisable()
        {
            if (_interactor != null)
                _interactor.OnTargetChanged -= HandleTargetChanged;
        }

        private void HandleTargetChanged(InteractableReceiver receiver)
        {
            if (receiver != null && _promptRoot != null)
            {
                _promptRoot.SetActive(true);
                if (_promptText != null)
                {
                    _promptText.text = $"[{_keyDisplayName}] {receiver.InteractionText}";
                }
            }
            else
            {
                if (_promptRoot != null) _promptRoot.SetActive(false);
            }
        }
    }
}
