/// <summary>
/// An object that defines its own interaction with a hero.
/// Attach an implementing component to the object or a parent of its collider.
/// </summary>
public interface IInteractable
{
    /// <summary>Whether this hero can interact with the object right now.</summary>
    bool CanInteract(Hero interactor);

    /// <summary>Perform the object's interaction for this hero.</summary>
    void Interact(Hero interactor);
}
