using UnityEngine;
using UnityEngine.UI;

/// <summary>선택된 저장 슬롯은 원본 아이콘 색을, 선택되지 않은 슬롯은 회색을 표시합니다.</summary>
public sealed class TitleSlotIconHoverView : MonoBehaviour
{
    Image _calendar, _phase;
    Material _calendarTint, _phaseTint;
    bool _selected;
    public void Configure(Image calendar, Image phase, bool morning)
    {
        if (_calendar != calendar) _calendarTint=calendar!=null?calendar.material:null;
        if (_phase != phase) _phaseTint=phase!=null?phase.material:null;
        _calendar=calendar; _phase=phase; Apply();
    }
    public void SetSelected(bool selected) { _selected=selected; Apply(); }
    void Apply()
    {
        Color grey=new Color32(152,148,134,255);
        if(_calendar!=null) {
            _calendar.material=_selected?null:_calendarTint;
            _calendar.color=_selected?Color.white:grey;
        }
        if(_phase!=null) {
            _phase.material=_selected?null:_phaseTint;
            _phase.color=_selected?Color.white:grey;
        }
    }
}
