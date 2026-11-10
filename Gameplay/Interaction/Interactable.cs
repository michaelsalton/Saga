using UnityEngine;
using UnityEngine.Events;

namespace Saga.Gameplay
{
    [DisallowMultipleComponent]
    public class Interactable : MonoBehaviour, IInteractable
    {
        [SerializeField] string prompt = "Use";
        [SerializeField] UnityEvent interacted;

        // public setter so an event entry can rename it after use
        public string Prompt { get => prompt; set => prompt = value; }

        public void Interact(Interactor interactor) => interacted.Invoke();
    }
}
