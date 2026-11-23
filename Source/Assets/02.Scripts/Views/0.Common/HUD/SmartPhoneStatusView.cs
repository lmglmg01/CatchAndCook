using TMPro;
using UnityEngine;

/// <summary>스마트폰의 시간대별 시각과 음악 설정 볼륨을 표시합니다. 게임 시간과 소리 설정은 변경하지 않습니다.</summary>
public sealed class SmartPhoneStatusView : MonoBehaviour
{
    #region 표시 연결
    [SerializeField] TMP_Text _clockText;
    [SerializeField] UnityEngine.UI.Image[] _musicBars;
    [SerializeField] SettingPreferencesView _preferences;
    [SerializeField] string _morningTime = "09:00 AM";
    [SerializeField] string _afternoonTime = "03:00 PM";
    [SerializeField] string _eveningTime = "06:00 PM";
    [SerializeField] Color _barColor = new Color32(194,167,139,255);
    [SerializeField, Range(0,1)] float _inactiveAlpha = .18f;
    GameManager _timeSource;
    BaseView _baseView;
    float _nextRefresh, _lastVolume = -1;
    #endregion

    #region 활성화와 현재 상태 복원
    void OnEnable()
    {
        _baseView = GetComponentInParent<BaseView>();
        if (_preferences == null && _baseView != null)
            _preferences = _baseView.transform.root.GetComponentInChildren<SettingPreferencesView>(true);
        _timeSource = GameManager.Instance;
        if (_timeSource != null)
        {
            _timeSource.DayPhaseChanged -= DisplayPhase;
            _timeSource.DayPhaseChanged += DisplayPhase;
        }
        _lastVolume = -1;
        RefreshPresentation();
    }

    void OnDisable()
    {
        if (_timeSource != null) _timeSource.DayPhaseChanged -= DisplayPhase;
        _timeSource = null;
    }

    /// <summary>음량 변경 이벤트가 없는 기존 설정의 공개 값만 읽습니다. 스마트폰이 열려 있을 때만 갱신합니다.</summary>
    void LateUpdate()
    {
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + .1f;
        RefreshPresentation();
    }

    public void RefreshPresentation()
    {
        DisplayPhase(_timeSource != null ? _timeSource.CurrentDayPhase :
            _baseView != null ? _baseView.ClockPhase : DayPhase.Morning);
        DisplayMusicVolume(_preferences != null ? _preferences.MusicVolume : 1f);
    }
    #endregion

    #region 전달받은 값 표시
    /// <summary>게임 시간대에 해당하는 고정 시각만 표시합니다. PC 시간이나 임의 타이머는 사용하지 않습니다.</summary>
    public void DisplayPhase(DayPhase phase)
    {
        string time = phase == DayPhase.Morning ? _morningTime :
            phase == DayPhase.Afternoon ? _afternoonTime : _eveningTime;
        if (_clockText != null && _clockText.text != time) _clockText.text = time;
    }

    /// <summary>음악 볼륨 0~1을 세 막대에 나누어 표시합니다. 볼륨을 저장하거나 믹서를 변경하지 않습니다.</summary>
    public void DisplayMusicVolume(float volume)
    {
        volume = float.IsNaN(volume) ? 0 : Mathf.Clamp01(volume);
        if (Mathf.Approximately(_lastVolume, volume)) return;
        _lastVolume = volume;
        if (_musicBars == null) return;
        for (int i = 0; i < _musicBars.Length; i++)
        {
            if (_musicBars[i] == null) continue;
            var color = _barColor;
            color.a *= Mathf.Lerp(_inactiveAlpha, 1, Mathf.Clamp01(volume * _musicBars.Length - i));
            _musicBars[i].color = color;
        }
    }
    #endregion
}
