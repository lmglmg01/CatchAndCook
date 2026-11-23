using System.Collections.Generic;
using UnityEngine;

/// <summary>Tracks interaction triggers touching the hero's collider.</summary>
[DisallowMultipleComponent]
public class InteractableSensor : MonoBehaviour
{
    [Header("감지 콜라이더")]
    [SerializeField] Collider _collider;

    [Header("감지 레이어")]
    [SerializeField] LayerMask _layers;

    List<Collider> _contacts = new List<Collider>();

    void OnTriggerEnter(Collider other)
    {
        RegisterContact(other);
    }

    void OnTriggerStay(Collider other)
    {
        RegisterContact(other);
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsTouching(other)) _contacts.Remove(other);
    }

    void OnDisable()
    {
        _contacts.Clear();
    }

    void RegisterContact(Collider other)
    {
        if (!isActiveAndEnabled || !IsTouching(other)) return;
        if (!_contacts.Contains(other)) _contacts.Add(other);
    }

    bool IsTouching(Collider other)
    {
        if (_collider == null || !_collider.enabled
            || !_collider.gameObject.activeInHierarchy
            || other == null || !other.enabled || !other.isTrigger
            || !other.gameObject.activeInHierarchy
            || (_layers.value & (1 << other.gameObject.layer)) == 0) return false;

        return Physics.ComputePenetration(
            _collider, _collider.transform.position, _collider.transform.rotation,
            other, other.transform.position, other.transform.rotation,
            out _, out _);
    }

    public IInteractable FindNearestInteractable(Hero interactor)
    {
        if (!isActiveAndEnabled || interactor == null || !interactor.isActiveAndEnabled
            || _collider == null) return null;

        Vector3 position = _collider.bounds.center;
        IInteractable nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = _contacts.Count - 1; i >= 0; i--)
        {
            Collider hit = _contacts[i];
            if (!IsTouching(hit))
            {
                _contacts.RemoveAt(i);
                continue;
            }
            if (hit.transform.IsChildOf(interactor.transform)) continue;

            Component component = hit.GetComponentInParent(typeof(IInteractable));
            if (!(component is IInteractable target) || component == null) continue;
            if (component is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
            if (!target.CanInteract(interactor)) continue;

            float distance = (hit.ClosestPoint(position) - position).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearest = target;
        }

        return nearest;
    }
}
