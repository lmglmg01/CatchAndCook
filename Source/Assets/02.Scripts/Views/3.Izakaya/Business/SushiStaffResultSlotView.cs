using UnityEngine;
using TMPro;

/// <summary>
/// 영업 정산에서 직원별 활동 내역 한 줄을 표시할 뷰입니다.
/// 현재는 인스펙터 연결용 변수만 선언하며 초기화·갱신·버튼 동작은 구현하지 않습니다.
/// </summary>
public class SushiStaffResultSlotView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 활동한 직원의 초상 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _portraitImage;
    // 직원 이름·담당 업무·서빙 또는 요리 횟수를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _activityText;

    #endregion

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 직원 초상과 활동 결과 텍스트를 표시합니다.
    /// </summary>
    /// <param name="portrait">초상화 이미지입니다.</param>
    /// <param name="activity">활동 값입니다.</param>
    Sprite _emptyPortrait;
    Color _emptyPortraitColor;
    void Awake()
    {
        if (_portraitImage == null) return;
        _emptyPortrait = _portraitImage.sprite;
        _emptyPortraitColor = _portraitImage.color;
    }

    /// <summary>초상이 없으면 기존 사진칸을 유지하고 실제 활동 수치를 표시합니다.</summary>
    public void Bind(Sprite portrait, string activity)
    {
        // 초상이 아직 없으면 기존 사진 배경을 유지합니다.
        if (_portraitImage != null && portrait != null)
        {
            _portraitImage.sprite = portrait;
            _portraitImage.color = Color.white;
            _portraitImage.preserveAspect = true;
        }
        if (_portraitImage != null && portrait == null)
        {
            _portraitImage.sprite = _emptyPortrait;
            _portraitImage.color = _emptyPortraitColor;
        }
        if (_activityText != null)
        {
            _activityText.text = activity ?? string.Empty;
        }
        GetComponentInParent<SushiRestaurantStyleView>()?.ApplySettlementPortrait(transform);
    }

    #endregion

}
