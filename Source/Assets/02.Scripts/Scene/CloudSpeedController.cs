using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class CloudSpeedController : MonoBehaviour
{
    [Header("Cloud Motion")]
    [SerializeField] float speed;

    MeshFilter meshFilter;
    Mesh originalMesh;
    Mesh movingMesh;
    Vector2[] originalUvs;
    Vector2[] movingUvs;
    float offset;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        originalMesh = meshFilter.sharedMesh;
        movingMesh = Instantiate(originalMesh);
        movingMesh.name = originalMesh.name + " (Moving Clouds)";
        meshFilter.sharedMesh = movingMesh;
        originalUvs = movingMesh.uv;
        movingUvs = new Vector2[originalUvs.Length];
    }

    void Update()
    {
        offset = Mathf.Repeat(offset + speed * Time.deltaTime, 1f);
        for (int i = 0; i < originalUvs.Length; i++)
        {
            movingUvs[i] = originalUvs[i] + Vector2.right * offset;
        }

        movingMesh.uv = movingUvs;
    }

    void OnDestroy()
    {
        if (meshFilter != null)
        {
            meshFilter.sharedMesh = originalMesh;
        }

        if (movingMesh != null)
        {
            Destroy(movingMesh);
        }
    }
}
