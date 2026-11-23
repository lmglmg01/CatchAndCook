using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
/// <summary>상세 제목이 영역을 넘는 경우 전체 제목을 툴팁으로 표시합니다.</summary>
public sealed class QuestTitleHoverView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    TMP_Text _text; QuestTitleTooltipView _tooltip;
    void Awake(){_text=GetComponent<TMP_Text>();_text.raycastTarget=true;}
    public void OnPointerEnter(PointerEventData e){
        if(_text==null)_text=GetComponent<TMP_Text>();
        _text.ForceMeshUpdate();
        var size=_text.GetPreferredValues(_text.text,Mathf.Infinity,Mathf.Infinity);
        if(!_text.isTextTruncated && !_text.isTextOverflowing && size.x<=_text.rectTransform.rect.width)return;
        var owner=GetComponentInParent<QuestDetailView>();if(owner==null)return;
        _tooltip=owner.GetComponent<QuestTitleTooltipView>()??owner.gameObject.AddComponent<QuestTitleTooltipView>();
        _tooltip.Show(this,_text.text);
    }
    public void OnPointerExit(PointerEventData e){if(_tooltip!=null)_tooltip.Hide(this);}
    void OnDisable(){if(_tooltip!=null)_tooltip.Hide(this);}
}
