using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>획득처 아이콘에서만 안내를 열고 아이콘 바로 위에 최상단으로 표시합니다.</summary>
public sealed class IngredientSourceIconView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] GameObject _tooltip;
    [SerializeField] RectTransform _avoidPanel;
    Transform _originalParent;
    Vector3 _originalPosition;
    int _originalIndex;
    bool _shown;
    readonly Vector3[] _corners = new Vector3[4];

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltip == null || _shown) return;
        var canvas = GetComponentInParent<Canvas>().rootCanvas;
        var root = canvas.transform as RectTransform;
        var tip = _tooltip.transform as RectTransform;
        _originalParent = tip.parent;
        _originalPosition = tip.localPosition;
        _originalIndex = tip.GetSiblingIndex();
        _shown = true;
        tip.SetParent(root, true);
        tip.SetAsLastSibling();
        var tooltipCanvas = _tooltip.GetComponent<Canvas>();
        if (tooltipCanvas == null) tooltipCanvas = _tooltip.AddComponent<Canvas>();
        tooltipCanvas.overrideSorting = true;
        tooltipCanvas.sortingLayerID = canvas.sortingLayerID;
        tooltipCanvas.sortingOrder = canvas.sortingOrder + 100;
        _tooltip.SetActive(true);
        Canvas.ForceUpdateCanvases();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        float width = tip.rect.width * Mathf.Abs(tip.lossyScale.x / root.lossyScale.x);
        float height = tip.rect.height * Mathf.Abs(tip.lossyScale.y / root.lossyScale.y);
        (transform as RectTransform).GetWorldCorners(_corners);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(camera, (_corners[1] + _corners[2]) * .5f), camera, out var iconTop);
        float x = Mathf.Clamp(iconTop.x - width * .5f, root.rect.xMin + 12f, Mathf.Max(root.rect.xMin + 12f, root.rect.xMax - width - 12f));
        float bottom = Mathf.Min(iconTop.y + 8f, root.rect.yMax - height - 12f);
        float top = bottom + height;
        tip.localPosition = new Vector3(x + width * tip.pivot.x, top - height * (1f - tip.pivot.y), 0f);
    }

    void OnEnable()
    {
        var image = GetComponent<UnityEngine.UI.Image>();
        if (image != null) image.raycastTarget = true;
    }

    public void OnPointerExit(PointerEventData eventData) => Hide();
    void OnDisable() => Hide();
    void Hide()
    {
        if (!_shown || _tooltip == null) return;
        _tooltip.SetActive(false);
        var tip = _tooltip.transform;
        if (_originalParent != null)
        {
            tip.SetParent(_originalParent, true);
            tip.localPosition = _originalPosition;
            tip.SetSiblingIndex(_originalIndex);
        }
        _shown = false;
    }
}
