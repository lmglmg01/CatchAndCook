using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>캐릭터·의자 UI의 사용 가능 여부만 알립니다. 주문·조리·청소 상태는 판단하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed class SushiWorldCanvasPresenter : MonoBehaviour
{
    [SerializeField] MonoBehaviour _owner;
    static readonly HashSet<SushiWorldCanvasPresenter> ActiveViews = new();
    public static event Action<SushiWorldCanvasPresenter> Available;
    public static event Action<SushiWorldCanvasPresenter> Unavailable;
    public MonoBehaviour Owner => _owner;

    /// <summary>초기화 순서와 관계없이 이미 활성화된 대상도 Presenter에 전달합니다.</summary>
    public static SushiWorldCanvasPresenter[] GetActiveViews()
    {
        ActiveViews.RemoveWhere(view => view == null);
        var result = new SushiWorldCanvasPresenter[ActiveViews.Count];
        ActiveViews.CopyTo(result); return result;
    }

    /// <summary>프리팹 또는 기존 씬의 대상 참조를 지정합니다. 같은 대상 재연결도 중복 생성 없이 처리됩니다.</summary>
    public void BindOwner(MonoBehaviour owner)
    {
        if (_owner == owner) return;
        bool registered = Application.isPlaying && ActiveViews.Remove(this);
        if (registered) Unavailable?.Invoke(this);
        _owner = owner;
        if (Application.isPlaying && isActiveAndEnabled && _owner != null)
        { ActiveViews.Add(this); Available?.Invoke(this); }
    }

    /// <summary>기존 프리팹의 누락된 대상 참조는 같은 오브젝트에서 한 번만 찾습니다.</summary>
    void Awake()
    {
        if (_owner != null) return;
        _owner = GetComponent<GuestNPC>();
        if (_owner == null) _owner = GetComponent<CookerNPC>();
        if (_owner == null) _owner = GetComponent<SushiCarrier>();
        if (_owner == null) _owner = GetComponent<Chair>();
    }

    /// <summary>생성·풀 재사용 시 UI 연결을 알립니다. 매 프레임 검색하지 않습니다.</summary>
    void OnEnable()
    {
        if (!Application.isPlaying || _owner == null) return;
        if (ActiveViews.Add(this)) Available?.Invoke(this);
    }

    /// <summary>풀 반환·비활성화 시 표시와 구독을 정리할 수 있도록 알립니다.</summary>
    void OnDisable()
    {
        if (ActiveViews.Remove(this)) Unavailable?.Invoke(this);
    }

    /// <summary>도메인 재로드를 생략하는 플레이에서도 이전 씬의 참조와 구독을 남기지 않습니다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() { ActiveViews.Clear(); Available = null; Unavailable = null; }
}
