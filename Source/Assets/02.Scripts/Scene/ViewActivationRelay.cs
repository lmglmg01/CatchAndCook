using System;
using UnityEngine;

// 기존 뷰를 수정하지 않고 창 활성화를 연결 계층에 알린다.
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class ViewActivationRelay : MonoBehaviour
{
    public event Action Opened;
    public event Action Closed;

    void OnEnable() => Opened?.Invoke();

    void OnDisable()
    {
        if (!gameObject.activeSelf && gameObject.scene.isLoaded) Closed?.Invoke();
    }
}