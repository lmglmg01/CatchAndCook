using UnityEngine;

/// <summary>슬롯 안쪽 크기는 유지하면서 바깥쪽에 6 기준 픽셀의 둥근 선택 테두리를 그립니다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UISelectionStrokeView : UnityEngine.UI.MaskableGraphic
{
    [SerializeField] float _radius = 30f;
    const float Thickness = 6f;

    /// <summary>기존 슬롯의 참조와 클릭 영역을 바꾸지 않고 선택 표시만 갱신합니다.</summary>
    public static void SetSelected(Transform slot, bool selected, float radius)
    {
        var child = slot.Find("SelectionStroke");
        if (child == null)
        {
            child = new GameObject("SelectionStroke", typeof(RectTransform), typeof(CanvasRenderer), typeof(UISelectionStrokeView)).transform;
            child.SetParent(slot, false);
        }
        if (child.GetComponent<CanvasRenderer>() == null) child.gameObject.AddComponent<CanvasRenderer>();
        var rect = (RectTransform)child;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        // 메시가 RectMask2D 경계에 너무 일찍 잘리지 않도록 외곽선까지 영역에 포함합니다.
        rect.offsetMin = Vector2.one * -Thickness; rect.offsetMax = Vector2.one * Thickness;
        child.SetAsFirstSibling();
        var view = child.GetComponent<UISelectionStrokeView>();
        view._radius = radius;
        view.color = new Color32(253, 247, 228, 255);
        view.raycastTarget = false;
        child.gameObject.SetActive(selected);
        view.SetVerticesDirty();
    }

    /// <summary>안쪽과 바깥쪽 호를 같은 중심으로 그려 모서리까지 일정한 두께를 유지합니다.</summary>
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        var outer = rectTransform.rect;
        var inner = new Rect(outer.x + Thickness, outer.y + Thickness, outer.width - Thickness * 2, outer.height - Thickness * 2);
        float radius = Mathf.Clamp(_radius, 0, Mathf.Min(inner.width, inner.height) * .5f);
        const int Segments = 16;
        int count = 4 * (Segments + 1);
        for (int corner = 0; corner < 4; corner++)
        {
            var center = new Vector2(corner < 2 ? inner.xMax - radius : inner.xMin + radius,
                corner == 0 || corner == 3 ? inner.yMax - radius : inner.yMin + radius);
            for (int step = 0; step <= Segments; step++)
            {
                float angle = (90 - corner * 90 - step * 90f / Segments) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + direction * radius, color, Vector2.zero);
                mesh.AddVert(center + direction * (radius + Thickness), color, Vector2.zero);
            }
        }
        for (int i = 0; i < count; i++)
        {
            int a = i * 2, b = ((i + 1) % count) * 2;
            mesh.AddTriangle(a, b, a + 1); mesh.AddTriangle(a + 1, b, b + 1);
        }
    }
}


