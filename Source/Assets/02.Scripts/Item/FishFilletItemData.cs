using UnityEngine;

[CreateAssetMenu(fileName = "FishFilletItemData", menuName = "Game/Item/Fish Fillet")]
public class FishFilletItemData : ItemData
{
    [Header("획득 경험치 (필렛 1개당)")]
    [SerializeField, Min(0f)] float experienceOnAcquire = 1f;

    [Header("무게 (필렛 1개당)")]
    [SerializeField, Min(0f)] float weight;

    [Header("물고기 데이터")]
    [SerializeField] FishData _data;

    public float Weight => weight;

    /// <summary>
    /// 연결된 물고기의 출현 수심을 반환합니다. 연결이 없으면 빈 문자열을 반환합니다.
    /// </summary>
    public string Depth
    {
        get
        {
            // 연결된 물고기 데이터가 없으면 미등록 상태로 전달합니다.
            if (_data == null)
            {
                return string.Empty;
            }

            // 물고기 데이터에 설정된 출현 수심을 반환합니다.
            return _data.Depth;
        }
    }


    public float ExperienceOnAcquire =>
        float.IsNaN(experienceOnAcquire) || float.IsInfinity(experienceOnAcquire)
            ? 0f : Mathf.Max(0f, experienceOnAcquire);
}
