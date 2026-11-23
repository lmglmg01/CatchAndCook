using System.Collections.Generic;
using UnityEngine;

/// <summary>자식 UI의 활성/비활성 이벤트를 연출 View에 전달합니다. 매 프레임 상태를 감지하지 않습니다.</summary>
[DisallowMultipleComponent, AddComponentMenu("")]
public sealed class UIDirectorTargetView : MonoBehaviour
{
    readonly HashSet<UIDirectorView> _owners = new();
    public void Register(UIDirectorView owner) { if(owner!=null)_owners.Add(owner); }
    void OnEnable() { Notify(true); }
    void OnDisable() { Notify(false); }
    void Notify(bool visible)
    {
        foreach(var owner in _owners)if(owner!=null)owner.TargetVisibilityChanged(transform as RectTransform,visible);
    }
}
