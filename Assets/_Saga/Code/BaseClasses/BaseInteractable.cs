using _Saga.Code.Interfaces;
using _Saga.Code.Player;
using UnityEngine;
using TMPro;

namespace _Saga.Code.BaseClasses
{
    public class BaseInteractable : MonoBehaviour, IInteractionInterface
    {
        [SerializeField] private string interactionText;
        [SerializeField] private GameObject floatingTextPrefab;
        
        private bool _isInteractable = true;
        private GameObject _floatingTextInstance;
        private TextMeshProUGUI _floatingText;
        
        public string InteractionText { get => interactionText; set => interactionText = value; }
        public GameObject FloatingTextPrefab { get => floatingTextPrefab; set => floatingTextPrefab = value; }
        public bool IsInteractable { get => _isInteractable; set => _isInteractable = value; }

        private void Awake()
        {
            InitializeText();
        }

        public virtual void Interact(PlayerCharacter caller){}
        
        public void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }
            if (_floatingTextInstance == null)
            {
                return;
            }
            _floatingTextInstance.SetActive(true);
        }
        
        public void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }
            if (_floatingTextInstance == null)
            {
                return;
            }
            _floatingTextInstance.SetActive(false);
        }

        public void InitializeText()
        {
            if (floatingTextPrefab == null)
            {
                Debug.LogError("Floating text prefab is null");
                return;
            }
            if (_floatingTextInstance == null)
            {
                var spawnPosition = SetTextSpawnPosition();
                _floatingTextInstance = Instantiate(floatingTextPrefab, spawnPosition, Quaternion.identity, transform);
                _floatingTextInstance.SetActive(false);
                _floatingText = _floatingTextInstance.GetComponentInChildren<TextMeshProUGUI>();
            }
            if (_floatingText != null)
            {
                _floatingText.text = interactionText;
            }
        }
        
        public Vector3 SetTextSpawnPosition()
        {
            var textRenderer = GetComponent<Renderer>();
            if (textRenderer != null)
            {
                var height = textRenderer.bounds.size.y;
                return transform.position + new Vector3(0, height + 0.5f, 0);
            }
            return transform.position + Vector3.up * 1f;
        }
    }
}