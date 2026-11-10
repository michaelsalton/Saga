namespace Saga.Gameplay
{
    public interface IInteractable
    {
        // verb phrase for the HUD; a property so a door can switch between "Open" and "Close"
        string Prompt { get; }
        void Interact(Interactor interactor);
    }
}
