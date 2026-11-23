using System;
using System.Collections.Generic;
using UnityEngine;

public class BoatNPC : NPC
{
    [Header("히어로 감지")]
    [SerializeField] Collider _detectionCollider;

    [Header("애니메이션")]
    [SerializeField] AnimatorHandler _animatorHandler;

    Dictionary<Collider, Hero> _contacts = new Dictionary<Collider, Hero>();
    List<Collider> _invalidContacts = new List<Collider>();

    public event Action<Hero> InteractionRequested;

    public event Action<Hero, bool> HeroDetectionChanged;

    public bool IsHeroDetected(Hero hero)
    {
        return isActiveAndEnabled && CanInteract(hero) && _contacts.ContainsValue(hero);
    }

    void FixedUpdate()
    {
        RemoveInvalidContacts();
    }


    void OnTriggerEnter(Collider other)
    {
        DetectHero(other);
    }

    void OnTriggerStay(Collider other)
    {
        DetectHero(other);
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsDetectionContact(other)) RemoveContact(other);
    }

    void OnDisable()
    {
        _invalidContacts.Clear();
        _invalidContacts.AddRange(_contacts.Keys);
        foreach (Collider contact in _invalidContacts)
            RemoveContact(contact);
        _invalidContacts.Clear();
    }

    void DetectHero(Collider other)
    {
        RemoveInvalidContacts();
        if (_contacts.ContainsKey(other) || !IsDetectionContact(other)) return;

        Hero hero = other.GetComponentInParent<Hero>();
        if (!CanInteract(hero)) return;

        bool alreadyDetected = _contacts.ContainsValue(hero);
        _contacts.Add(other, hero);
        if (alreadyDetected) return;

        if (_animatorHandler != null)
            _animatorHandler.SetBoatNPCInteract();
        HeroDetectionChanged?.Invoke(hero, true);
    }

    void RemoveInvalidContacts()
    {
        _invalidContacts.Clear();
        foreach (var contact in _contacts)
        {
            if (!IsDetectionContact(contact.Key) || !CanInteract(contact.Value))
                _invalidContacts.Add(contact.Key);
        }

        foreach (Collider contact in _invalidContacts)
            RemoveContact(contact);
    }


    void RemoveContact(Collider contact)
    {
        if (!_contacts.TryGetValue(contact, out Hero hero)) return;

        _contacts.Remove(contact);
        if (!_contacts.ContainsValue(hero))
            HeroDetectionChanged?.Invoke(hero, false);
    }

    bool IsDetectionContact(Collider other)
    {
        if (!isActiveAndEnabled || _detectionCollider == null
            || !_detectionCollider.enabled || !_detectionCollider.isTrigger
            || !_detectionCollider.gameObject.activeInHierarchy
            || other == null || !other.enabled || !other.gameObject.activeInHierarchy)
            return false;

        return Physics.ComputePenetration(
            _detectionCollider, _detectionCollider.transform.position, _detectionCollider.transform.rotation,
            other, other.transform.position, other.transform.rotation,
            out _, out _);
    }

    protected override void OnInteract(Hero interactor)
    {
        InteractionRequested?.Invoke(interactor);
    }
}
