using System.Collections.Generic;
using UnityEngine;

public class InteractObjectSpawner : MonoBehaviour
{
    [Header("생성 위치")]
    [SerializeField] Transform[] _spawnPoints;

    [Header("생성 프리팹")]
    [SerializeField] GameObject _o2CanisterPrefab;
    [SerializeField] GameObject _bulletBoxPrefab;

    [Header("생성 수")]
    [SerializeField, Min(0)] int _o2CanisterSpawnCount;
    [SerializeField, Min(0)] int _bulletBoxSpawnCount;

    void Start()
    {
        List<Transform> availablePoints = GetAvailablePoints();
        int o2Remaining = GetSpawnCount(_o2CanisterPrefab, _o2CanisterSpawnCount, "O2Canister");
        int bulletRemaining = GetSpawnCount(_bulletBoxPrefab, _bulletBoxSpawnCount, "BulletBox");

        if ((long)o2Remaining + bulletRemaining > availablePoints.Count)
            Debug.LogWarning("상호작용 오브젝트의 생성 위치가 부족하여 일부만 생성합니다.", this);

        while (availablePoints.Count > 0 && (o2Remaining > 0 || bulletRemaining > 0))
        {
            int pointIndex = Random.Range(0, availablePoints.Count);
            Transform spawnPoint = availablePoints[pointIndex];
            int lastIndex = availablePoints.Count - 1;
            availablePoints[pointIndex] = availablePoints[lastIndex];
            availablePoints.RemoveAt(lastIndex);

            bool spawnO2 = bulletRemaining == 0
                || (o2Remaining > 0
                    && Random.value < (double)o2Remaining / ((long)o2Remaining + bulletRemaining));
            GameObject prefab = spawnO2 ? _o2CanisterPrefab : _bulletBoxPrefab;
            Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

            if (spawnO2)
                o2Remaining--;
            else
                bulletRemaining--;
        }
    }

    List<Transform> GetAvailablePoints()
    {
        var points = new List<Transform>();
        if (_spawnPoints == null) return points;

        var positions = new HashSet<Vector3>();
        foreach (Transform point in _spawnPoints)
        {
            if (point != null && positions.Add(point.position))
                points.Add(point);
        }

        return points;
    }

    int GetSpawnCount(GameObject prefab, int requestedCount, string objectName)
    {
        if (requestedCount <= 0) return 0;
        if (prefab != null) return requestedCount;

        Debug.LogWarning(objectName + " 프리팹이 연결되지 않아 생성하지 않습니다.", this);
        return 0;
    }
}
