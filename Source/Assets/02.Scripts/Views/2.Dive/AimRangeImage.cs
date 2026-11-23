using UnityEngine;

[AddComponentMenu("UI/Aim Range Image")]
public class AimRangeImage : UnityEngine.UI.Image
{

    #region 멤버

    #region 런타임 상태

    float _thickness; // 두께 값입니다.

    #endregion

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 조준 범위의 두께가 바뀌면 메시 갱신을 요청합니다.
    /// </summary>
    /// <param name="thickness">두께 값입니다.</param>
    public void SetThickness(float thickness)
    {
        thickness = Mathf.Max(0f, thickness);
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_thickness == thickness)
        {
            return;
        }

        _thickness = thickness;
        SetVerticesDirty();
    }

    /// <summary>
    /// 채움 비율에 맞는 고리 형태의 조준 메시를 구성합니다.
    /// </summary>
    /// <param name="vertices">메시 정점 값입니다.</param>
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertices)
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (type != Type.Filled || fillMethod != FillMethod.Radial360)
        {
            base.OnPopulateMesh(vertices);
            return;
        }

        vertices.Clear();
        Rect rect = GetPixelAdjustedRect(); // 사각 영역 값입니다.
        float outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f; // 바깥쪽 반경 값입니다.
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (outerRadius <= 0f || fillAmount <= 0f || _thickness <= 0f)
        {
            return;
        }

        float innerRadius = Mathf.Max(0f, outerRadius - _thickness); // 안쪽 반경 값입니다.
        float sweep = fillAmount * 360f; // 호의 각도 값입니다.
        int segments = Mathf.Max(1, Mathf.CeilToInt(sweep / 2f)); // 분할 구간 값입니다.
        float startAngle = fillOrigin * 90f - 90f; // 시작 각도 값입니다.
        float sign = fillClockwise ? -1f : 1f; // 부호 값입니다.
        Color32 tint = color; // 색상 값입니다.
        // i: 반복 값입니다.
        for (int i = 0; i <= segments; i++)
        {
            float radians = (startAngle + sign * sweep * i / segments) * Mathf.Deg2Rad; // 라디안 각도 값입니다.
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)); // 방향 값입니다.
            vertices.AddVert(rect.center + direction * outerRadius, tint, Vector2.zero);
            vertices.AddVert(rect.center + direction * innerRadius, tint, Vector2.zero);
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (i == 0)
            {
                continue;
            }

            int previous = (i - 1) * 2; // 이전 값입니다.
            vertices.AddTriangle(previous, previous + 2, previous + 1);
            vertices.AddTriangle(previous + 2, previous + 3, previous + 1);
        }
    }

    #endregion

}
