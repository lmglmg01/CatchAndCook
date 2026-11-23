using UnityEngine;

public abstract class InteractObjectData : ScriptableObject
{
    [Header("상호작용 오브젝트 정보")]
    [SerializeField] string _interactObjectId;
    [SerializeField] string _interactObjectName;

    public string InteractObjectId => _interactObjectId;
    public string InteractObjectName => _interactObjectName;
}
