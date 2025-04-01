using _Saga.Code.BaseClasses;
using _Saga.Code.Player;
using UnityEngine;

namespace _Saga.Code.Interactables
{
    public class ChestInteractable : BaseInteractable
    {
        [SerializeField] private string usedText = "Empty Chest";
        [SerializeField] private bool isOpened = false;
        
        public override void Interact(PlayerCharacter caller)
        {
            if (isOpened)
            {
                return;
            }
            isOpened = true;
            InteractionText = usedText;
            InitializeText();
        }
    }
}