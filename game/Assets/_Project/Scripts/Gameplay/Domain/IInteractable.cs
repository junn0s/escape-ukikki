using UnityEngine;

namespace MonkeyLab.Gameplay.Domain
{
    public interface IInteractable
    {
        string Prompt { get; }
        Transform InteractionTransform { get; }
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
