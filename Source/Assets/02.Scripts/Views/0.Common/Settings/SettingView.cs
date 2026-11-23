using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 소리·화면 모드를 표시하고 키보드 설정 페이지로 이동하는 설정 메인 뷰입니다.
/// </summary>
public class SettingView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [Header("메인 페이지")]
    [SerializeField] Button _closeMainButton; // 기존 Settings_Main/CloseButton을 연결합니다.
    [SerializeField] GameObject _mainWindow; // 기존 Settings_Main 패널을 연결합니다.
    [SerializeField] SettingPreferencesView _preferences; // 같은 공통 Canvas 루트의 설정 저장 뷰를 연결합니다.

    [Header("소리")]
    [SerializeField] Slider _musicSlider; // 음악 행의 Slider를 연결합니다. 값 범위는 0~1입니다.
    [SerializeField] Slider _effectsSlider; // 효과음 행의 Slider를 연결합니다. 값 범위는 0~1입니다.
    [SerializeField] TMP_Text _musicVolumeText; // 음악 행 오른쪽의 백분율 TMP_Text를 연결합니다.
    [SerializeField] TMP_Text _effectsVolumeText; // 효과음 행 오른쪽의 백분율 TMP_Text를 연결합니다.
    [SerializeField] bool _plainVolumeText; // 사진 기준 배치에서는 켭니다. 볼륨을 % 기호 없이 0~100 숫자로 표시합니다.

    [Header("화면 모드")]
    [SerializeField] Button _windowModeButton; // 창 모드 Button을 연결합니다.
    [SerializeField] Button _fullScreenButton; // 전체 화면 Button을 연결합니다.
    [SerializeField] TMP_Text _screenModeText; // 현재 모드를 안내할 TMP_Text를 연결합니다.

    [Header("키보드")]
    [SerializeField] GameObject _keyboardWindow; // 기존 Settings_Keyboard 패널을 연결합니다.
    [SerializeField] Button _openKeyboardSettingButton; // 기존 KeyboardArea/OpenButton을 연결합니다.
    [SerializeField] KeyboardSettingView _keyboardView; // Settings_Keyboard의 키 설정 뷰를 연결합니다.
    [SerializeField] bool _inlineKeyboard; // Canvas_Board 전용 통합 배치. Base의 기존 페이지 동작은 보존합니다.
    [SerializeField] SettingScreenView _boardScreen; // 선상 화면 표시를 연결할 뷰입니다.

    #endregion

    #region 런타임 상태

    bool _openedKeyboardPage = false; // 현재 키 설정 페이지 표시 여부입니다. UI 연결은 필요 없습니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 같은 화면 키보드 여부입니다.
    /// </summary>
    public bool InlineKeyboard => _inlineKeyboard;

    /// <summary>
    /// 표시 전체 화면 여부입니다.
    /// </summary>
    public bool DisplayFullScreen => _preferences != null ? _preferences.FullScreen : Screen.fullScreen;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 기존 버튼과 새 슬라이더·화면 모드 버튼을 한 번 연결합니다.
    /// </summary>
    void Awake()
    {
        UIEscapeView.Register(this, _closeMainButton);
        if (_closeMainButton != null)
        {
            _closeMainButton.onClick.AddListener(Close);
        }

        if (_openKeyboardSettingButton != null)
        {
            _openKeyboardSettingButton.onClick.AddListener(OpenKeyboard);
        }

        if (_musicSlider != null)
        {
            _musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (_effectsSlider != null)
        {
            _effectsSlider.onValueChanged.AddListener(SetEffectsVolume);
        }

        if (_windowModeButton != null)
        {
            _windowModeButton.onClick.AddListener(SetWindowMode);
        }

        if (_fullScreenButton != null)
        {
            _fullScreenButton.onClick.AddListener(SetFullScreenMode);
        }
    }

    /// <summary>
    /// 설정을 열면 메인 페이지로 돌아가 저장값을 표시합니다.
    /// </summary>
    void OnEnable()
    {
        SetKeyboardSetting(false);
        Refresh();
    }

    /// <summary>
    /// 외부 앱 전환으로 닫혀도 키 변경 대기와 입력 잠금을 해제합니다.
    /// </summary>
    void OnDisable()
    {
        if (_keyboardView != null)
        {
            _keyboardView.CancelRebind();
        }
    }

    /// <summary>
    /// 이 뷰가 연결한 UI 이벤트를 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_closeMainButton != null)
        {
            _closeMainButton.onClick.RemoveListener(Close);
        }

        if (_openKeyboardSettingButton != null)
        {
            _openKeyboardSettingButton.onClick.RemoveListener(OpenKeyboard);
        }

        if (_musicSlider != null)
        {
            _musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
        }

        if (_effectsSlider != null)
        {
            _effectsSlider.onValueChanged.RemoveListener(SetEffectsVolume);
        }

        if (_windowModeButton != null)
        {
            _windowModeButton.onClick.RemoveListener(SetWindowMode);
        }

        if (_fullScreenButton != null)
        {
            _fullScreenButton.onClick.RemoveListener(SetFullScreenMode);
        }
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 인라인 키 설정을 정리하고 설정 화면을 닫습니다.
    /// </summary>
    public void CloseInlineSettings()
    {
        Close();
    }

    /// <summary>
    /// 단축키 설정 버튼에서 키보드 페이지를 엽니다.
    /// </summary>
    void OpenKeyboard()
    {
        SetKeyboardSetting(true);
    }

    /// <summary>
    /// 현재 앱을 닫고 키 변경 대기를 종료합니다.
    /// </summary>
    void Close()
    {
        SetMainSetting(false);
    }

    /// <summary>
    /// 창 모드 버튼의 선택을 적용합니다.
    /// </summary>
    void SetWindowMode()
    {
        if (_preferences != null)
        {
            _preferences.SetFullScreen(false);
        }

        Refresh();
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 기존 설정 창 표시 함수를 유지합니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    void SetMainSetting(bool isOn)
    {
        var fade = GetComponent<AppFadeView>(); // 페이드 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (fade != null && fade.TrySetVisible(isOn, () => SetMainSetting(isOn)))
        {
            return;
        }

        gameObject.SetActive(isOn);
    }

    /// <summary>
    /// 메인과 키보드 페이지가 겹치지 않도록 한 페이지만 표시합니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    void SetKeyboardSetting(bool isOn)
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_inlineKeyboard)
        {
            _openedKeyboardPage = false;
            if (_mainWindow != null)
            {
                _mainWindow.SetActive(true);
            }

            if (_keyboardWindow != null)
            {
                _keyboardWindow.SetActive(true);
            }

            return;
        }

        _openedKeyboardPage = isOn;
        if (_keyboardWindow != null)
        {
            _keyboardWindow.SetActive(isOn);
        }

        if (_mainWindow != null)
        {
            _mainWindow.SetActive(!isOn);
        }
    }

    /// <summary>
    /// 키 설정 페이지의 뒤로 버튼에서 메인 설정으로 돌아갑니다.
    /// </summary>
    public void ReturnToMain()
    {
        SetKeyboardSetting(false);
        Refresh();
    }

    /// <summary>
    /// 슬라이더 이벤트를 재호출하지 않고 저장된 설정을 화면에 반영합니다.
    /// </summary>
    public void Refresh()
    {
        float music = _preferences != null ? _preferences.MusicVolume : 1f; // 음악 값입니다.
        float effects = _preferences != null ? _preferences.EffectsVolume : 1f; // 효과음 값입니다.
        if (_musicSlider != null)
        {
            _musicSlider.SetValueWithoutNotify(music);
        }

        if (_effectsSlider != null)
        {
            _effectsSlider.SetValueWithoutNotify(effects);
        }

        if (_musicVolumeText != null)
        {
            _musicVolumeText.text = Mathf.RoundToInt(music * 100f).ToString() + (_plainVolumeText ? string.Empty : "%");
        }

        if (_effectsVolumeText != null)
        {
            _effectsVolumeText.text = Mathf.RoundToInt(effects * 100f).ToString() + (_plainVolumeText ? string.Empty : "%");
        }

        bool fullScreen = _preferences != null ? _preferences.FullScreen : Screen.fullScreen; // 전체 화면 여부입니다.
        if (_windowModeButton != null)
        {
            _windowModeButton.interactable = fullScreen;
        }

        if (_fullScreenButton != null)
        {
            _fullScreenButton.interactable = !fullScreen;
        }

        if (_screenModeText != null)
        {
            _screenModeText.text = fullScreen ? "전체 화면 · 테두리 없음" : "창 모드";
        }

        if (_boardScreen != null)
        {
            _boardScreen.RefreshPresentation();
        }
    }

    /// <summary>
    /// 음악 슬라이더의 값을 저장하고 백분율을 표시합니다.
    /// </summary>
    /// <param name="value">현재 표시 항목에 적용할 값입니다.</param>
    void SetMusicVolume(float value)
    {
        if (_preferences != null)
        {
            _preferences.SetMusicVolume(value);
        }

        Refresh();
    }

    /// <summary>
    /// 효과음 슬라이더의 값을 저장하고 백분율을 표시합니다.
    /// </summary>
    /// <param name="value">현재 표시 항목에 적용할 값입니다.</param>
    void SetEffectsVolume(float value)
    {
        if (_preferences != null)
        {
            _preferences.SetEffectsVolume(value);
        }

        Refresh();
    }

    /// <summary>
    /// 전체 화면 버튼의 선택을 적용합니다.
    /// </summary>
    void SetFullScreenMode()
    {
        if (_preferences != null)
        {
            _preferences.SetFullScreen(true);
        }

        Refresh();
    }

    #endregion

}
