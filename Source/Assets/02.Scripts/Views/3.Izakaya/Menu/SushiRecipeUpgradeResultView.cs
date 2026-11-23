using UnityEngine;
using TMPro;

/// <summary>
/// 요리 강화 결과 안내를 표시할 팝업 뷰입니다.
/// 버튼은 Awake에서 연결하며 화면 표시만 전환합니다. 실제 게임 데이터 처리는 연결 전입니다.
/// </summary>
public class SushiRecipeUpgradeResultView : MonoBehaviour
{

    [System.Serializable]
    public sealed class ResultData
    {
        public bool IsStaff;
        public Sprite Icon, StatIcon, UnlockIcon;
        public string Name, StatLabel, BeforeValue, AfterValue, UnlockMessage;
        public int BeforeLevel, Level;
        public bool ShowUnlock;
    }
    ResultData _result;
    public GameObject PageRoot => _pageRoot;
    public ResultData Result => _result;

    /// <summary>확정된 표시 데이터만 받아 배치하며 강화 처리는 실행하지 않습니다.</summary>
    public void BindResult(ResultData result)
    {
        if(result==null)return;
        _result=result;
        IjakayaView.SetDisplayIcon(_foodIconImage,result.Icon);
        if(_completeMessageText!=null)_completeMessageText.text=result.Name??string.Empty;
        ApplyResultStyle();
    }
    void ApplyResultStyle()
    {
        if(_result==null)return;
        GetComponentInParent<SushiRestaurantStyleView>()?.ApplyUpgradeComplete(this,_result);
        // 비활성 팝업에 먼저 바인딩한 경우에도 새 배지·버튼의 렌더링을 갱신합니다.
        if(_pageRoot!=null)foreach(var graphic in _pageRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
        {
            graphic.SetAllDirty();
            if(graphic is TMP_Text text)text.ForceMeshUpdate(true,true);
        }
    }
    void OnEnable(){ApplyResultStyle();}

    #region 멤버

    #region UI 연결 및 표시 설정

    // 요리 이름을 포함한 강화 완료 안내 텍스트를 연결합니다.
    [SerializeField] TMP_Text _completeMessageText;
    // 강화가 완료된 요리 아이콘 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _foodIconImage;
    // 최종 요리 이름·레벨·수치를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _upgradedStatsText;
    // 강화 결과를 확인하고 팝업을 닫을 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _confirmButton;
    // 이 버튼으로 닫을 화면의 최상위 GameObject를 연결합니다. 팝업은 반투명 배경까지 포함한 루트입니다.
    [SerializeField] GameObject _pageRoot;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 이 View가 처음 활성화될 때 버튼 이벤트를 연결합니다.
    /// </summary>
    void Awake()
    {
        UIEscapeView.Register(this, _confirmButton);
        InitializeButtons();
    }

    /// <summary>
    /// 이 View가 등록한 버튼 이벤트만 제거합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(Close);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 다른 이벤트를 유지하면서 이 View의 버튼 콜백을 중복 없이 연결합니다.
    /// </summary>
    void InitializeButtons()
    {
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(Close);
            _confirmButton.onClick.AddListener(Close);
        }
    }

    /// <summary>
    /// 실제 완료 결과를 전달받아 표시합니다. 표시만 하며 강화를 실행하지 않습니다.
    /// </summary>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="message">메시지 값입니다.</param>
    /// <param name="stats">능력치 값입니다.</param>
    public void Bind(Sprite icon, string message, string stats)
    {
        _result=new ResultData{Icon=icon,Name=(message??string.Empty).Replace(" 강화 완료",string.Empty),AfterValue=stats??string.Empty};
        IjakayaView.SetDisplayIcon(_foodIconImage, icon);
        if (_completeMessageText != null)
        {
            _completeMessageText.text = message ?? string.Empty;
        }

        if (_upgradedStatsText != null)
        {
            _upgradedStatsText.text = stats ?? string.Empty;
        }
        ApplyResultStyle();
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 다른 화면이나 데이터를 변경하지 않고 이 화면만 비활성화합니다.
    /// </summary>
    public void Close()
    {
        if (_pageRoot != null)
        {
            _pageRoot.SetActive(false);
        }
    }

    #endregion

}
