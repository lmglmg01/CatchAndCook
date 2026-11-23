using TMPro;
using UnityEngine;

/// <summary>퀘스트 화면의 보상 툴팁 하나를 공유하며 화면 밖으로 나가지 않도록 배치합니다.</summary>
public class QuestRewardTooltipView : MonoBehaviour
{
    private QuestRewardIconView _source;
    private RectTransform _panel;
    private RectTransform _canvasRect;
    private TMP_Text _label;
    private Canvas _canvas;

    /// <summary>특정 아이콘이 현재 열어 둔 툴팁인지 확인합니다.</summary>
    public bool IsShowing(QuestRewardIconView source)
    {
        return _source == source && _panel != null && _panel.gameObject.activeSelf;
    }

    /// <summary>아이콘이 요청한 문구를 표시하고 이름 길이에 맞춰 높이를 계산합니다.</summary>
    public void Show(QuestRewardIconView source, string content)
    {
        EnsurePanel();
        if (_panel == null) return;
        _source = source;
        float width = Mathf.Min(360f, _canvasRect.rect.width - 24f);
        _label.text = content;
        float height = Mathf.Max(62f, _label.GetPreferredValues(content, width - 32f, Mathf.Infinity).y + 28f);
        _panel.sizeDelta = new Vector2(width, height);
        _panel.gameObject.SetActive(true);
        _panel.SetAsLastSibling();
        FollowPointer();
    }

    /// <summary>다른 아이콘의 새 툴팁을 닫지 않도록 요청자를 확인합니다.</summary>
    public void Hide(QuestRewardIconView source)
    {
        if (_source != source) return;
        HideAll();
    }

    /// <summary>스크롤 영역의 마스크에 잘리지 않는 별도의 표시용 패널을 만듭니다.</summary>
    private void EnsurePanel()
    {
        if (_panel != null) return;
        var parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;
        _canvas = parentCanvas.rootCanvas;
        _canvasRect = _canvas.transform as RectTransform;
        var go = new GameObject("QuestRewardTooltip", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasGroup), typeof(UnityEngine.UI.Image));
        _panel = go.GetComponent<RectTransform>();
        _panel.SetParent(_canvas.transform, false);
        _panel.pivot = new Vector2(0, 1);
        var overlay = go.GetComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingLayerID = _canvas.sortingLayerID;
        overlay.sortingOrder = 32760;
        var group = go.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        var background = go.GetComponent<UnityEngine.UI.Image>();
        background.color = new Color(.015f, .16f, .23f, .98f);
        background.raycastTarget = false;
        var textObject = new GameObject("RewardInfo", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(_panel, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16, 14);
        textRect.offsetMax = new Vector2(-16, -14);
        _label = textObject.GetComponent<TMP_Text>();
        var existing = GetComponentInChildren<TMP_Text>(true);
        if (existing != null) _label.font = existing.font;
        _label.fontSize = 24;
        _label.color = new Color(253f / 255f, 247f / 255f, 228f / 255f, 1);
        _label.alignment = TextAlignmentOptions.MidlineLeft;
        _label.textWrappingMode = TextWrappingModes.Normal;
        _label.richText = false;
        _label.raycastTarget = false;
        go.SetActive(false);
    }

    /// <summary>표시 중 마우스 위치와 아이콘의 활성 상태를 매 프레임 확인합니다.</summary>
    private void LateUpdate()
    {
        if (_source == null || !_source.isActiveAndEnabled)
        {
            HideAll();
            return;
        }
        FollowPointer();
    }

    /// <summary>마우스 위치를 따라가며 오른쪽·아래쪽 끝에서도 패널이 화면 안에 있도록 보정합니다.</summary>
    private void FollowPointer()
    {
        if (_panel == null || !_panel.gameObject.activeSelf) return;
        Vector2 point;
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;
        point = mouse.position.ReadValue();
#else
        point = Input.mousePosition;
#endif
        Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, point, camera, out var local)) return;
        local += new Vector2(18, -18);
        Rect bounds = _canvasRect.rect;
        local.x = Mathf.Clamp(local.x, bounds.xMin + 12, bounds.xMax - _panel.rect.width - 12);
        local.y = Mathf.Clamp(local.y, bounds.yMin + _panel.rect.height + 12, bounds.yMax - 12);
        _panel.position = _canvasRect.TransformPoint(local);
    }

    /// <summary>현재 요청자와 패널 표시를 함께 초기화합니다.</summary>
    private void HideAll()
    {
        _source = null;
        if (_panel != null) _panel.gameObject.SetActive(false);
    }

    /// <summary>퀘스트 앱을 닫으면 Canvas 아래의 툴팁도 숨깁니다.</summary>
    private void OnDisable() => HideAll();

    /// <summary>씬 또는 퀘스트 화면이 제거될 때 만들어 둔 패널을 정리합니다.</summary>
    private void OnDestroy()
    {
        if (_panel != null) Destroy(_panel.gameObject);
    }
}