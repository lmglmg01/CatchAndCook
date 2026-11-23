using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>씬에서 사용하는 프리팹별 객체를 준비하고 대여·반환합니다.</summary>
[DisallowMultipleComponent]
public class PoolManager : MonoBehaviour
{
    [Header("풀 사전 준비")]
    [Tooltip("Prepare 호출들이 한 프레임에 새로 생성할 수 있는 총 개수입니다. 미설정 시 1개씩 준비합니다.")]
    [SerializeField, Min(1)] int _prepareCountPerFrame;

    sealed class Pool
    {
        public GameObject Prefab;
        public readonly Queue<PoolItem> Available = new();
        public readonly HashSet<PoolItem> Items = new();
    }

    sealed class PoolItem
    {
        public GameObject Instance;
        public Pool Pool;
        public MonoBehaviour[] Callbacks;
        public Vector3 LocalScale;
        public bool IsRented;
    }

    readonly Dictionary<GameObject, Pool> _pools = new();
    readonly Dictionary<GameObject, PoolItem> _items = new();
    Transform _storageRoot;
    Transform _activeRoot;
    int _prepareFrame = -1;
    int _preparedThisFrame;
    bool _disposing;

    /// <summary>
    /// 대여 중인 객체를 포함하여 해당 프리팹을 최소 count개 확보합니다.
    /// 호출자는 yield return Prepare(...)로 준비 완료를 기다린 뒤 게임을 시작합니다.
    /// 이미 확보한 수량은 다시 만들지 않으며, 여러 호출도 프레임당 생성 예산을 공유합니다.
    /// </summary>
    public IEnumerator Prepare(GameObject prefab, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        Pool pool = GetPool(prefab);
        while (!_disposing && CountAlive(pool) < count)
        {
            if (_prepareFrame != Time.frameCount)
            {
                _prepareFrame = Time.frameCount;
                _preparedThisFrame = 0;
            }

            if (_preparedThisFrame >= Mathf.Max(1, _prepareCountPerFrame))
            {
                yield return null;
                continue;
            }

            PoolItem item = CreateItem(pool);
            pool.Available.Enqueue(item);
            _preparedThisFrame++;
        }
    }

    /// <summary>
    /// OnSpawn → 월드 위치·회전 설정 → initialize → 활성화 순서로 대여합니다.
    /// initialize에서 총알의 방향·속도·데미지 등 이번 사용 값을 전달합니다.
    /// 풀에 남은 객체가 없으면 즉시 추가 생성하므로, 빈번한 생성은 Prepare로 미리 줄입니다.
    /// </summary>
    public GameObject Get(GameObject prefab, Vector3 worldPosition, Quaternion worldRotation,
        Transform parent = null, Action<GameObject> initialize = null)
    {
        Pool pool = GetPool(prefab);
        PoolItem item = TakeItem(pool);
        GameObject instance = item.Instance;
        item.IsRented = true;

        try
        {
            instance.transform.SetParent(parent != null ? parent : _activeRoot, false);
            instance.transform.localScale = item.LocalScale;
            foreach (MonoBehaviour callback in item.Callbacks)
                if (callback != null) ((IPoolable)callback).OnSpawn();

            instance.transform.SetPositionAndRotation(worldPosition, worldRotation);
            initialize?.Invoke(instance);
            if (instance == null || !item.IsRented)
                throw new InvalidOperationException("풀 초기화 도중 객체를 파괴하거나 반환할 수 없습니다.");

            instance.SetActive(true);
            return instance;
        }
        catch
        {
            // 초기화에 실패한 객체를 다음 대여에 재사용하지 않습니다.
            if (item.IsRented)
            {
                item.IsRented = false;
                try { Despawn(item); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
            Discard(item);
            throw;
        }
    }

    /// <summary>이 매니저가 대여한 루트 객체만 반환합니다. 중복·타 풀 반환은 false입니다.</summary>
    public bool Return(GameObject instance)
    {
        if (_disposing || ReferenceEquals(instance, null)
            || !_items.TryGetValue(instance, out PoolItem item) || !item.IsRented) return false;

        // OnDespawn/OnDisable에서 중복 반환을 요청해도 다시 처리하지 않습니다.
        item.IsRented = false;
        if (instance == null)
        {
            Discard(item);
            return false;
        }

        try
        {
            Despawn(item);
            instance.SetActive(false);
            instance.transform.SetParent(_storageRoot, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = item.LocalScale;
            item.Pool.Available.Enqueue(item);
            return true;
        }
        catch
        {
            Discard(item);
            throw;
        }
    }

    public int GetTotalCount(GameObject prefab)
    {
        return prefab != null && _pools.TryGetValue(prefab, out Pool pool) ? CountAlive(pool) : 0;
    }

    public int GetAvailableCount(GameObject prefab)
    {
        if (prefab == null || !_pools.TryGetValue(prefab, out Pool pool)) return 0;
        int count = 0;
        foreach (PoolItem item in pool.Items)
            if (item.Instance != null && !item.IsRented) count++;
        return count;
    }

    Pool GetPool(GameObject prefab)
    {
        if (_disposing) throw new InvalidOperationException("정리 중인 풀은 사용할 수 없습니다.");
        if (prefab == null) throw new ArgumentNullException(nameof(prefab));
        if (prefab.scene.IsValid())
            throw new ArgumentException("씬 객체 대신 프로젝트의 프리팹 에셋을 전달하세요.", nameof(prefab));

        EnsureRoots();
        if (!_pools.TryGetValue(prefab, out Pool pool))
        {
            pool = new Pool { Prefab = prefab };
            _pools.Add(prefab, pool);
        }
        return pool;
    }

    void EnsureRoots()
    {
        if (_storageRoot == null)
        {
            var storage = new GameObject("Pool_Stored");
            storage.SetActive(false);
            storage.transform.SetParent(transform, false);
            _storageRoot = storage.transform;
        }
        if (_activeRoot == null)
        {
            _activeRoot = new GameObject("Pool_Active").transform;
            _activeRoot.SetParent(transform, false);
        }
    }

    PoolItem CreateItem(Pool pool)
    {
        // 비활성 부모 아래에서 생성하여 초기화 전 OnEnable 실행을 막습니다.
        GameObject instance = Instantiate(pool.Prefab, _storageRoot, false);
        instance.SetActive(false);
        var item = new PoolItem
        {
            Instance = instance,
            Pool = pool,
            LocalScale = instance.transform.localScale,
            Callbacks = Array.FindAll(instance.GetComponentsInChildren<MonoBehaviour>(true),
                component => component is IPoolable)
        };
        pool.Items.Add(item);
        _items.Add(instance, item);
        return item;
    }

    PoolItem TakeItem(Pool pool)
    {
        while (pool.Available.Count > 0)
        {
            PoolItem item = pool.Available.Dequeue();
            if (item.Instance != null && pool.Items.Contains(item)) return item;
            Discard(item);
        }
        return CreateItem(pool);
    }

    int CountAlive(Pool pool)
    {
        int count = 0;
        foreach (PoolItem item in pool.Items)
            if (item.Instance != null) count++;
        return count;
    }

    void Despawn(PoolItem item)
    {
        foreach (MonoBehaviour callback in item.Callbacks)
            if (callback != null) ((IPoolable)callback).OnDespawn();
    }

    void Discard(PoolItem item)
    {
        item.IsRented = false;
        item.Pool.Items.Remove(item);
        _items.Remove(item.Instance);
        if (item.Instance == null) return;
        item.Instance.SetActive(false);
        if (Application.isPlaying) Destroy(item.Instance);
        else DestroyImmediate(item.Instance);
    }

    void OnDestroy()
    {
        _disposing = true;
        // 다른 표시 지점에 부모가 연결된 대여 객체도 함께 정리합니다.
        foreach (PoolItem item in _items.Values)
        {
            if (item.Instance == null) continue;
            if (item.IsRented)
            {
                item.IsRented = false;
                try { Despawn(item); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
            if (item.Instance == null) continue;
            item.Instance.SetActive(false);
            if (Application.isPlaying) Destroy(item.Instance);
            else DestroyImmediate(item.Instance);
        }
        _items.Clear();
        _pools.Clear();
    }
}
