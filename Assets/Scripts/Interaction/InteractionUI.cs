using InteractionSystem;

namespace InteractionSystem
{
    using TMPro;
    using UnityEngine;

    /// <summary>
    /// PlayerInteractor のイベントを購読し、インタラクトプロンプトを表示する UI コントローラー
    /// </summary>
    public class InteractionUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private TextMeshProUGUI _promptText;
        [SerializeField] private GameObject _promptRoot; // 親オブジェクトで表示/非表示を制御

        private void Awake()
        {
            if (_promptRoot != null) _promptRoot.SetActive(false);
            
            if (_interactor == null)
            {
                _interactor = FindObjectOfType<PlayerInteractor>();
            }

            if (_interactor != null)
            {
                _interactor.OnTargetChanged += OnTargetChanged;
            }
            else
            {
                Debug.LogError("[InteractionUI] PlayerInteractor が見つかりません。", this);
            }
        }

        private void OnDestroy()
        {
            if (_interactor != null)
            {
                _interactor.OnTargetChanged -= OnTargetChanged;
            }
        }

        private void OnTargetChanged(IInteractable target)
        {
            if (target != null && _promptRoot != null)
            {
                _promptText.text = target.GetInteractText();
                _promptRoot.SetActive(true);
            }
            else if (_promptRoot != null)
            {
                _promptRoot.SetActive(false);
            }
        }
    }
}
