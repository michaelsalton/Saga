using _Saga.Code.Player;
using UnityEngine;

namespace _Saga.Code.Interfaces
{
    public interface IInteractionInterface
    {
        void Interact(PlayerCharacter caller);
        public void OnTriggerEnter(Collider other);
        public void OnTriggerExit(Collider other);
        void InitializeText();
        Vector3 SetTextSpawnPosition();
        
        string InteractionText { get; set; }
        GameObject FloatingTextPrefab { get; set; }
        bool IsInteractable { get; set; }
    }
}