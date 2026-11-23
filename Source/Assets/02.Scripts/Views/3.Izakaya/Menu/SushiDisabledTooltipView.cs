using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>비활성 버튼의 제한 사유를 마우스 호버 중 표시하는 임시 안내 뷰입니다.</summary>
public class SushiDisabledTooltipView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] string _kind = "register";
    UnityEngine.UI.Button _button;
    RectTransform _panel;
    TMP_Text _text;
    bool _hover;
    public void OnPointerEnter(PointerEventData e) { _hover = true; Refresh(); }
    public void OnPointerExit(PointerEventData e) { _hover = false; Hide(); }
    void LateUpdate() { if (_hover) Refresh(); }
    void OnApplicationFocus(bool focused) { if (!focused) { _hover = false; Hide(); } }
    void OnDisable() { _hover = false; Hide(); }
    void OnDestroy() { if (_panel != null) Destroy(_panel.gameObject); }
    void Hide() { if (_panel != null) _panel.gameObject.SetActive(false); }
    /// <summary>모델을 변경하지 않고 현재 선택과 수량의 제한 사유를 매 프레임 읽습니다.</summary>
    void Refresh()
    {
        if (_button == null) _button = GetComponent<UnityEngine.UI.Button>();
        if (_button == null || _button.IsInteractable()) { Hide(); return; }
        // Exit 이벤트가 누락되더라도 실제 마우스가 버튼 밖이면 즉시 닫습니다.
        var ownerCanvas = GetComponentInParent<Canvas>();
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (ownerCanvas == null || mouse == null) { _hover = false; Hide(); return; }
        var canvas = ownerCanvas.rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screen = mouse.position.ReadValue();
        if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, screen, camera))
        {
            _hover = false; Hide(); return;
        }
        var presenter = GetComponentInParent<IjakayaPresenter>();
        int amount = -1;
        var quantity = GetComponentInParent<SushiMenuQuantityView>();
        var input = quantity != null ? quantity.GetComponentInChildren<TMP_InputField>(true) : null;
        if (input != null && !int.TryParse(input.text, out amount)) amount = 0;
        string reason = presenter != null ? presenter.GetDisabledMenuReason(_kind, amount) : "현재 상태에서는 실행할 수 없습니다. 재료와 등록 수량을 확인하세요.";

        var parent = canvas.transform as RectTransform;
        if (_panel == null)
        {
            var go = new GameObject("TemporaryDisabledTooltip", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false); _panel = (RectTransform)go.transform;
            var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = new Color(.025f,.10f,.16f,.97f); image.raycastTarget = false;
            var label = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));label.transform.SetParent(go.transform,false);
            _text = label.GetComponent<TMP_Text>();var sample=GetComponentInChildren<TMP_Text>(true);if(sample!=null)_text.font=sample.font;
            _text.fontSize=23;_text.color=new Color32(253,247,228,255);_text.alignment=TextAlignmentOptions.TopLeft;_text.raycastTarget=false;
            var tr=_text.rectTransform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(18,14);tr.offsetMax=new Vector2(-18,-14);
        }
        _panel.gameObject.SetActive(true);_panel.SetAsLastSibling();_text.text=reason;
        float h=Mathf.Clamp(_text.GetPreferredValues(reason,404,0).y+28,70,400);_panel.sizeDelta=new Vector2(440,h);


        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,screen,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out var point);
        point+=new Vector2(235,-h*.5f-22);point.x=Mathf.Clamp(point.x,parent.rect.xMin+228,parent.rect.xMax-228);point.y=Mathf.Clamp(point.y,parent.rect.yMin+h*.5f+8,parent.rect.yMax-h*.5f-8);
        _panel.localPosition=point;
    }
}
