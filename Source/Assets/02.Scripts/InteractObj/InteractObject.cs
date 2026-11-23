using UnityEngine;

[DisallowMultipleComponent]
public abstract class InteractObject : MonoBehaviour, IInteractable
{
    [Header("상호작용 뷰")]
    [SerializeField] ObjectInteractView _objectInteractView;

    [Header("상호작용 콜라이더")]
    [SerializeField] Collider _interactionCollider;

    [Header("상호작용 상태")]
    [SerializeField] bool _isInteracted;

    [Header("이펙트")]
    [SerializeField] GameObject _effect;

    public bool IsInteracted => _isInteracted;
    protected virtual bool IsSingleUse => true;

    protected virtual void Awake()
    {
        if (_isInteracted && _interactionCollider != null)
            _interactionCollider.enabled = false;
        SetDetected(false);
        if (_effect != null) _effect.SetActive(true);
    }

    protected virtual void OnDisable()
    {
        SetDetected(false);
    }

    public virtual bool CanInteract(Hero interactor)
    {
        return !_isInteracted
            && _interactionCollider != null
            && _interactionCollider.enabled
            && _interactionCollider.isTrigger
            && _interactionCollider.gameObject.activeInHierarchy
            && isActiveAndEnabled
            && interactor != null
            && interactor.isActiveAndEnabled;
    }

    public void Interact(Hero interactor)
    {
        if (!CanInteract(interactor) || !ApplyInteraction(interactor)) return;
        if (interactor is UnderSeaHero) GameSfx.Play(GameSoundId.UseInteractable);
        if (!IsSingleUse) return;

        _isInteracted = true;
        if (_interactionCollider != null) _interactionCollider.enabled = false;
        SetDetected(false);
        FinishUse(_effect);
    }

    protected virtual void FinishUse(GameObject effect)
    {
        if (effect != null) effect.SetActive(false);
    }

    public void SetDetected(bool detected)
    {
        if (_objectInteractView == null) return;

        if (detected && !_isInteracted && isActiveAndEnabled)
            _objectInteractView.Show(true);
        else
            _objectInteractView.Hide();
    }

    protected abstract bool ApplyInteraction(Hero interactor);
}
