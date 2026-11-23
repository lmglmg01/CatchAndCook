using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>서빙·음식 처리·테이블 정리의 표시만 구성합니다. 주문, 입력, 처리 시간은 변경하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed class SushiCleanupBubbleView : MonoBehaviour
{
    [Header("표시 연결")]
    [SerializeField] GameObject _layoutRoot, _legacyRoot, _gaugeRoot;
    [SerializeField] Image _bubble, _tail, _icon, _labelBackground, _gaugeTrack, _gaugeFill;
    [SerializeField] TMP_Text _label;
    [SerializeField] RectTransform _originalBubbleAnchor;
    [Header("교체할 리소스 — 말풍선은 꼬리를 포함한 전체 이미지")]
    [SerializeField] Sprite _servingBubbleSprite, _discardBubbleSprite, _cleaningBubbleSprite;
    [SerializeField] Sprite _discardProcessingBubbleSprite, _cleaningProcessingBubbleSprite;
    [SerializeField] Sprite _discardIconSprite, _cleaningIconSprite;
    [SerializeField] Sprite _idleLabelSprite, _processingLabelSprite;
    [SerializeField] Sprite _gaugeTrackSprite, _gaugeFillSprite, _roundedShape;
    [Header("신규 리소스 수령 전 임시 배경색")]
    [SerializeField] Color _servingColor = new Color32(87,130,154,255);
    [SerializeField] Color _actionColor = new Color32(253,247,228,255);
    [SerializeField] Color _labelColor = new Color32(87,130,154,255);
    [SerializeField] Color _progressColor = new Color32(101,232,242,255);

    Sprite _runtimeGaugeSprite, _runtimeGaugeSource, _runtimeServingTail, _runtimeTailSource;
    bool _isCleaning;

    static void Place(RectTransform r,float x,float y,float width,float height)
    {
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
        r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,height);
    }
    void Rounded(Image image,Sprite sprite,Color tint,float radius)
    {
        image.sprite=sprite!=null?sprite:_roundedShape;
        if(image==_gaugeFill&&image.sprite!=null&&image.sprite.border.x<=0)
            image.sprite=GaugeSprite(image.sprite);
        image.color=tint;image.type=Image.Type.Sliced;image.preserveAspect=false;image.fillCenter=true;
        if(image.sprite!=null&&image.sprite.border.x>0)image.pixelsPerUnitMultiplier=image.sprite.border.x/Mathf.Max(.01f,radius);
    }
    /// <summary>기존 View에서 받은 상태만 표시하고, 스프라이트는 Inspector에서 교체합니다.</summary>
    public void Present(bool visible,Sprite food,bool cleaning,bool processing,bool showLabel,float progress,string key)
    {
        if(_layoutRoot==null)return;
        _isCleaning=cleaning;
        if(_legacyRoot!=null)_legacyRoot.SetActive(false);
        _layoutRoot.SetActive(visible);if(!visible)return;
        var bubbleSprite=cleaning?(processing?_cleaningProcessingBubbleSprite:_cleaningBubbleSprite)
            :processing?_discardProcessingBubbleSprite:_servingBubbleSprite;
        var color=cleaning||processing?_actionColor:_servingColor;
        float labelY=178,gaugeY=158,gaugeWidth=94,gaugeHeight=10;
        bool integrated=IsIntegrated(bubbleSprite)&&(processing||showLabel);
        if(integrated)
        {
            float bodyCenter=773.5f,diameter=951f,headerCenter=processing?177.5f:188.5f;
            if(bubbleSprite.name=="icon_bubble_white1"){bodyCenter=743;diameter=990;headerCenter=154.5f;}
            else if(bubbleSprite.name=="icon_bubble_white1_with_gaugeback"){bodyCenter=731.5f;diameter=960;headerCenter=150;}
            float scale=96f/diameter;
            Place(_bubble.rectTransform,0,112+(bodyCenter-bubbleSprite.rect.height*.5f)*scale,
                bubbleSprite.rect.width*scale,bubbleSprite.rect.height*scale);
            _bubble.sprite=bubbleSprite;_bubble.type=Image.Type.Simple;_bubble.preserveAspect=true;_bubble.color=Color.white;
            _tail.gameObject.SetActive(false);labelY=112+(bodyCenter-headerCenter)*scale;
            float trackCenter=bubbleSprite.name.Contains("white1")?340f:386.5f;
            gaugeY=112+(bodyCenter-trackCenter)*scale;
            gaugeWidth=(bubbleSprite.name.Contains("white1")?900f:891f)*scale;
            gaugeHeight=(bubbleSprite.name.Contains("white1")?80f:79f)*scale;
        }
        else if(bubbleSprite!=null&&!IsIntegrated(bubbleSprite))
        {
            Place(_bubble.rectTransform,0,104,96,112);_bubble.sprite=bubbleSprite;
            _bubble.type=Image.Type.Simple;_bubble.preserveAspect=true;_bubble.color=Color.white;_tail.gameObject.SetActive(false);
        }
        else
        {
            // 통합 원화의 라벨이 원의 윗부분을 덮으므로 잘라 쓰지 않고 온전한 원과 원본 꼬리를 합칩니다.
            Place(_bubble.rectTransform,0,112,96,96);Rounded(_bubble,_roundedShape,color,48);
            _tail.gameObject.SetActive(true);
            if(!processing&&IsIntegrated(bubbleSprite))
            {
                // 키 안내가 없는 상태에는 라벨을 제외하고 같은 원화의 꼬리만 사용합니다.
                bool white=bubbleSprite.name.Contains("white1");float diameter=white?990f:951f;
                float center=white?743f:773.5f,tailHeight=white?141f:146f,scale=96f/diameter;
                if(_runtimeServingTail==null||_runtimeTailSource!=bubbleSprite)
                {
                    if(_runtimeServingTail!=null)Destroy(_runtimeServingTail);
                    _runtimeServingTail=Sprite.Create(bubbleSprite.texture,
                        new Rect(bubbleSprite.rect.x+380,bubbleSprite.rect.y,240,tailHeight),new Vector2(.5f,.5f),
                        bubbleSprite.pixelsPerUnit,0,SpriteMeshType.FullRect);_runtimeTailSource=bubbleSprite;
                }
                Place(_tail.rectTransform,0,112+(center-bubbleSprite.rect.height+tailHeight*.5f)*scale,240*scale,tailHeight*scale);
                _tail.rectTransform.localRotation=Quaternion.identity;_tail.sprite=_runtimeServingTail;
                _tail.type=Image.Type.Simple;_tail.color=Color.white;
            }
            else
            {
                Place(_tail.rectTransform,0,61,17,17);Rounded(_tail,_roundedShape,color,2);
                _tail.rectTransform.localRotation=Quaternion.Euler(0,0,45);
            }
        }
        var icon=cleaning?_cleaningIconSprite:processing?_discardIconSprite:food;
        _icon.sprite=icon;_icon.color=Color.white;_icon.enabled=icon!=null;_icon.type=Image.Type.Simple;_icon.preserveAspect=true;
        if(icon!=null)FitIcon(icon);
        _labelBackground.gameObject.SetActive(showLabel||processing);
        var labelSprite=processing?_processingLabelSprite:_idleLabelSprite;
        Place(_labelBackground.rectTransform,0,labelY,90,30);
        Rounded(_labelBackground,labelSprite,labelSprite!=null?Color.white:_labelColor,12);
        _labelBackground.enabled=!integrated;
        // 정리 대기 안내는 고정 폭 말풍선에 Space만 표시합니다. 입력·작업 시간은 기존 로직을 유지합니다.
        _label.text=processing?(cleaning?"정리 중":"처리 중"):cleaning?"Space":(key??"G")+" 처리";
        _label.color=integrated&&bubbleSprite.name.Contains("bluegray")?_servingColor:_actionColor;
        _gaugeRoot.SetActive(processing);
        if(!processing)return;
        progress=float.IsNaN(progress)||float.IsInfinity(progress)?0:Mathf.Clamp01(progress);
        Place((RectTransform)_gaugeRoot.transform,0,gaugeY,integrated?gaugeWidth:96,integrated?gaugeHeight:12);
        Place(_gaugeTrack.rectTransform,0,0,integrated?gaugeWidth:96,integrated?gaugeHeight:12);
        Rounded(_gaugeTrack,_gaugeTrackSprite,_labelColor,integrated?gaugeHeight*.5f:6);_gaugeTrack.enabled=!integrated;
        float width=gaugeWidth*progress;
        var r=_gaugeFill.rectTransform;r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.pivot=new Vector2(0,.5f);
        r.anchoredPosition=new Vector2(-gaugeWidth*.5f,0);r.sizeDelta=new Vector2(width,gaugeHeight);
        Rounded(_gaugeFill,_gaugeFillSprite,_gaugeFillSprite!=null&&_gaugeFillSprite!=_roundedShape?Color.white:_progressColor,Mathf.Min(width,gaugeHeight)*.5f);_gaugeFill.enabled=progress>0;
    }
    static bool IsIntegrated(Sprite sprite) => sprite!=null&&(sprite.name=="icon_bubble_bluegray"||sprite.name=="icon_bubble_bluegray_with_gaugeback"||sprite.name=="icon_bubble_white1"||sprite.name=="icon_bubble_white1_with_gaugeback");
    Sprite GaugeSprite(Sprite source)
    {
        if(_runtimeGaugeSprite!=null&&_runtimeGaugeSource==source)return _runtimeGaugeSprite;
        if(_runtimeGaugeSprite!=null)Destroy(_runtimeGaugeSprite);
        float border=Mathf.Min(source.rect.width,source.rect.height)*.5f;
        // 원본 리소스는 그대로 두고, 둥근 채움이 가로로 찌그러지지 않게 런타임9-slice로 표시합니다.
        _runtimeGaugeSprite=Sprite.Create(source.texture,source.rect,new Vector2(.5f,.5f),source.pixelsPerUnit,0,
            SpriteMeshType.FullRect,Vector4.one*border);_runtimeGaugeSource=source;return _runtimeGaugeSprite;
    }
    void OnDestroy()
    {
        if(_runtimeGaugeSprite!=null)Destroy(_runtimeGaugeSprite);
        if(_runtimeServingTail!=null)Destroy(_runtimeServingTail);
    }

    // 말풍선 중심의 기존 높이는 유지하면서 캐릭터·의자의 상속 배율 차이만 보정합니다.
    void LateUpdate()
    {
        if(_layoutRoot==null||!_layoutRoot.activeInHierarchy)return;
        var camera=Camera.main;var r=_layoutRoot.transform as RectTransform;
        if(camera==null||r==null||r.parent==null)return;
        var center=r.parent.TransformPoint(new Vector3(0,112,0));
        if(camera.WorldToScreenPoint(center).z<=.001f)return;
        var top=camera.WorldToScreenPoint(r.parent.TransformPoint(new Vector3(0,162,0)));
        var bottom=camera.WorldToScreenPoint(r.parent.TransformPoint(new Vector3(0,62,0)));
        float pixelsPerUnit=Mathf.Abs(top.y-bottom.y)/100f;
        if(pixelsPerUnit<.0001f)return;
        // 좌석 잔반은 주문·만족도 표시보다 작게 표시하며 안내와 게이지도 함께 축소합니다.
        float scale=(camera.pixelHeight/1080f)/pixelsPerUnit*(_isCleaning?.75f:1f);
        r.localScale=Vector3.one*scale;
        var target=_originalBubbleAnchor!=null?_originalBubbleAnchor.TransformPoint(_originalBubbleAnchor.rect.center)
            :r.parent.TransformPoint(new Vector3(0,112,0));
        var centerInRoot=(Vector3)r.rect.center+new Vector3(0,112,0);
        r.position=target-r.TransformVector(centerInRoot);
    }

    void FitIcon(Sprite sprite)
    {
        if(!IconBounds.TryGetValue(sprite.name,out var bounds))bounds=new Rect(0,0,sprite.rect.width,sprite.rect.height);
        float scale=67.2f/Mathf.Max(bounds.width,bounds.height);
        var r=_icon.rectTransform;Place(r,0,112,sprite.rect.width*scale,sprite.rect.height*scale);
        r.anchoredPosition+=new Vector2(sprite.rect.width*.5f-bounds.center.x,bounds.center.y-sprite.rect.height*.5f)*scale;
    }
    static readonly System.Collections.Generic.Dictionary<string,Rect> IconBounds = new()
    {
        ["EightbandedButterflyfish_Nigiri"]=new Rect(54,74,148,106),
        ["MantaRay_Fry"]=new Rect(26,60,212,144),
        ["SkunkTilefish_Nigiri"]=new Rect(56,88,151,85),
        ["WhaleShark_Nigiri"]=new Rect(45,67,166,119),
        ["RedSeahorse_Nigiri"]=new Rect(54,77,148,100),
        ["BlueTangNigiri"]=new Rect(61,88,131,83),
        ["Watanabe_Nigiri"]=new Rect(57,81,142,96),
        ["IndusRiverDolphin_Nigiri"]=new Rect(63,79,143,96),
        ["BlueTang_Gunkan"]=new Rect(64,81,128,99),
        ["BlueTang_Grilled"]=new Rect(67,57,120,143),
        ["IndusRiverDolphin_Grilled"]=new Rect(67,59,123,142),
        ["IndusRiverDolphin_Gunkan"]=new Rect(64,69,128,116),
        ["EightbandedButterflyfish_Grilled"]=new Rect(58,58,139,142),
        ["RedSeaHorse_Grilled"]=new Rect(63,45,130,169),
        ["MantaRay_Nigiri"]=new Rect(31,67,195,121),
        ["WhaleShark_Fry"]=new Rect(23,55,205,163),
        ["WhaleShark_Grilled"]=new Rect(19,84,215,120),
        ["WhaleShark_Stake"]=new Rect(39,78,186,118),
        ["SkunkTileFish_Gunkan"]=new Rect(66,77,124,103),
        ["SkunkTileFish_Roll"]=new Rect(75,68,106,115),
        ["SkunkTileFish_Grilled"]=new Rect(60,59,134,136),
        ["Watanabe_Grilled"]=new Rect(65,58,124,141),
        ["Eel_Fry_Lit"]=new Rect(75,186,490,293),
        ["Eel_Skewer_Lit"]=new Rect(85,236,476,167),
        ["CucumberPlate"]=new Rect(32,69,187,106),
        ["Tea"]=new Rect(54,37,148,173),
        ["BeerMug"]=new Rect(45,21,147,219),
        ["Icon_Trash_1"]=new Rect(141,34,720,931),
        ["Icon_Trash_2"]=new Rect(141,34,720,931),
        ["Icon_Garbage_1"]=new Rect(45,65,910,870),
        ["Icon_Garbage_2"]=new Rect(45,65,910,870),
    };
}

