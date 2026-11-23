using UnityEngine;

// Add a trigger Collider and set Area Id to an ExploreQuestData.TargetAreaId.
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public sealed class QuestAreaTrigger : MonoBehaviour
{
    [SerializeField] string _areaId;

    void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null) trigger.isTrigger = true;
    }

    void OnTriggerEnter(Collider other) => Report(other);
    void OnTriggerStay(Collider other) => Report(other);

    void Report(Collider other)
    {
        if (string.IsNullOrEmpty(_areaId) || other.GetComponentInParent<UnderSeaHero>() == null)
            return;
        GameManager.Instance?.QuestController?.ReportAreaEntered(_areaId);
    }
}
