using UnityEngine;

// Add a trigger Collider on the Interactable layer and set Object Id to a quest target.
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public sealed class QuestInteractableObject : MonoBehaviour, IInteractable
{
    [SerializeField] string _objectId;
    [SerializeField] Collider _interactionCollider;
    bool _interacted;

    void Reset()
    {
        _interactionCollider = GetComponent<Collider>();
        if (_interactionCollider != null) _interactionCollider.isTrigger = true;
    }

    void Awake()
    {
        if (_interactionCollider == null) _interactionCollider = GetComponent<Collider>();
    }

    public bool CanInteract(Hero interactor)
    {
        QuestController quests = GameManager.Instance != null
            ? GameManager.Instance.QuestController : null;
        return !_interacted && interactor is UnderSeaHero
            && _interactionCollider != null && _interactionCollider.enabled
            && _interactionCollider.isTrigger && gameObject.activeInHierarchy
            && quests != null && quests.HasActiveObjectTarget(_objectId);
    }

    public void Interact(Hero interactor)
    {
        if (!CanInteract(interactor)) return;
        GameManager.Instance.QuestController.ReportObjectInteracted(_objectId);
        _interacted = true;
        _interactionCollider.enabled = false;
    }
}
