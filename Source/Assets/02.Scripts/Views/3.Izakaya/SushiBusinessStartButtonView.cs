using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>영업시작 버튼의 위치와 기본·호버 원화 표시만 담당합니다.</summary>
public sealed class SushiBusinessStartButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    Button _button;
    Image _image, _backdrop;
    TMP_Text _label;
    RectTransform _rect;
    bool _hovered, _blocked;
    CanvasGroup _inputGroup;
    Tween _scaleTween;
    float _scaleTarget=1f;
    Sprite _normalSprite, _hoverSprite;
    static readonly Color Normal = new Color32(202,145,103,255);
    static readonly Color Hover = new Color32(248,184,109,255);

    public void Configure(Button button, Transform canvasRoot, GameObject guide, Sprite normalSprite, Sprite hoverSprite)
    {
        _button=button;_rect=(RectTransform)button.transform;
        // 튜토리얼이 버튼 자체에 설정한 잠금은 건드리지 않고 별도 부모에서 팝업 입력만 차단합니다.
        var gate=canvasRoot.Find("BusinessStartInputGate") as RectTransform;
        if(gate==null){var go=new GameObject("BusinessStartInputGate",typeof(RectTransform),typeof(CanvasGroup));go.layer=button.gameObject.layer;gate=go.GetComponent<RectTransform>();gate.SetParent(canvasRoot,false);}
        gate.anchorMin=Vector2.zero;gate.anchorMax=Vector2.one;gate.offsetMin=gate.offsetMax=Vector2.zero;
        _inputGroup=gate.GetComponent<CanvasGroup>();
        _normalSprite=normalSprite;_hoverSprite=hoverSprite;
        _image=button.GetComponent<Image>()??button.gameObject.AddComponent<Image>();
        _image.sprite=_normalSprite;_image.type=Image.Type.Simple;_image.raycastTarget=true;_image.preserveAspect=true;
        button.targetGraphic=_image;button.transition=Selectable.Transition.None;
        _label=button.GetComponentInChildren<TMP_Text>(true);
        if(_label==null){var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(button.transform,false);_label=go.GetComponent<TMP_Text>();}
        _label.gameObject.SetActive(true);_label.raycastTarget=false;_label.enableAutoSizing=false;
        _label.color=new Color32(116,76,65,255);_label.alignment=TextAlignmentOptions.Midline;_label.margin=Vector4.zero;
        var labelRect=_label.rectTransform;labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
        _backdrop=guide.GetComponent<Image>()??guide.AddComponent<Image>();
        _backdrop.sprite=null;_backdrop.color=new Color32(57,48,52,166);_backdrop.raycastTarget=false;
        var gr=(RectTransform)guide.transform;gr.anchorMin=gr.anchorMax=gr.pivot=new Vector2(.5f,.5f);gr.anchoredPosition=Vector2.zero;gr.sizeDelta=new Vector2(1920,540);
        var old=guide.transform.Find("CenteredContent");if(old!=null)old.gameObject.SetActive(false);
        var warning=guide.transform.Find("BusinessPreparationWarning") as RectTransform;
        if(warning!=null){warning.anchorMin=warning.anchorMax=warning.pivot=new Vector2(.5f,.5f);warning.anchoredPosition=new Vector2(0,-255);warning.sizeDelta=new Vector2(1100,60);}
        // 같은 버튼을 재사용해 기존 onClick과 조작 가능 상태를 유지합니다.
        _rect.SetParent(_inputGroup.transform,false);_inputGroup.transform.SetAsLastSibling();
        _rect.anchorMin=_rect.anchorMax=_rect.pivot=new Vector2(.5f,.5f);
        Refresh(true,false,false);
    }

    public void Refresh(bool preparationVisible, bool bookVisible, bool blocked, Transform modalLayer = null)
    {
        if(_button==null)return;
        bool visible=preparationVisible;
        _blocked=blocked;
        if(_inputGroup!=null){_inputGroup.interactable=!blocked;_inputGroup.blocksRaycasts=!blocked;}
        // 팝업을 열어도 간판은 유지하고, 같은 Canvas에서 팝업보다 먼저 그립니다.
        var layer=_inputGroup!=null?_inputGroup.transform:_rect;
        if(modalLayer!=null && modalLayer.parent==layer.parent)
        {
            if(layer.GetSiblingIndex()>modalLayer.GetSiblingIndex())layer.SetSiblingIndex(modalLayer.GetSiblingIndex());
        }
        else if(bookVisible)
        {
            var book=layer.parent.Find("MenuBook");
            if(book!=null && layer.GetSiblingIndex()<book.GetSiblingIndex())layer.SetSiblingIndex(book.GetSiblingIndex());
        }
        if(gameObject.activeSelf!=visible)gameObject.SetActive(visible);
        if(_backdrop!=null)_backdrop.enabled=visible&&!bookVisible;
        _rect.anchoredPosition=bookVisible?new Vector2(-810,280):Vector2.zero;
        _rect.sizeDelta=bookVisible?new Vector2(180,180):new Vector2(460,460);
        _rect.localRotation=Quaternion.Euler(0,0,bookVisible?8f:0f);
        _label.fontSize=bookVisible?28:44;
        Render();
    }
    void Render()
    {
        if(_image==null)return;
        bool hover=_hovered&&_button.interactable&&!_blocked;
        var sprite=hover&&_hoverSprite!=null?_hoverSprite:_normalSprite;
        _image.sprite=sprite;
        _image.color=sprite!=null?(_button.interactable?Color.white:new Color(1,1,1,.6f)):(_button.interactable?(hover?Hover:Normal):new Color32(151,143,136,255));
        _label.gameObject.SetActive(sprite==null);
        _label.text=hover?"영업 시작!":"영업 시작";
        float target=hover?1.04f:1f;
        if(!Mathf.Approximately(_scaleTarget,target))
        {
            _scaleTarget=target;_scaleTween?.Kill();
            _scaleTween=_rect.DOScale(target,.14f).SetEase(Ease.OutSine).SetUpdate(true);
        }
    }
    public void OnPointerEnter(PointerEventData e){_hovered=true;Render();}
    public void OnPointerExit(PointerEventData e){_hovered=false;Render();}
    public void OnSelect(BaseEventData e){_hovered=true;Render();}
    public void OnDeselect(BaseEventData e){_hovered=false;Render();}
    void OnDisable()
    {
        _hovered=false;_scaleTween?.Kill();_scaleTween=null;_scaleTarget=1f;
        if(_rect!=null)_rect.localScale=Vector3.one;
    }
    void OnDestroy(){_scaleTween?.Kill();}
}
