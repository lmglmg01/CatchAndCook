using UnityEngine;

[DisallowMultipleComponent]
public abstract class NPC : MonoBehaviour, IInteractable
{
    public virtual bool CanInteract(Hero interactor)
    {
        return isActiveAndEnabled && interactor != null && interactor.isActiveAndEnabled;
    }

    public void Interact(Hero interactor)
    {
        if (!CanInteract(interactor)) return;

        OnInteract(interactor);
    }

    protected abstract void OnInteract(Hero interactor);
}
