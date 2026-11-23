using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

/// <summary>
/// 공통 Canvas가 생성될 때 저장된 소리·화면·키 표시 설정을 불러오는 설정 전용 뷰입니다.
/// </summary>
public class SettingPreferencesView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] AudioMixer _audioMixer; // SettingsAudio 믹서를 연결합니다. UI 컴포넌트가 아닌 음악·효과음 설정 대상입니다.
    [SerializeField] InputActionAsset _inputActions; // 실제 InputHandler에서 참조하는 InputSystem_Actions 에셋을 연결합니다.
    [SerializeField] string _musicParameter = "MusicVolume"; // 믹서에서 노출한 음악 볼륨 매개변수 이름입니다.
    [SerializeField] string _effectsParameter = "EffectsVolume"; // 믹서에서 노출한 효과음 볼륨 매개변수 이름입니다.

    #endregion

    #region 런타임 상태

    const string Prefix = "CTeam.Settings.v1."; // 다른 저장값과 구분하는 설정 키 접두사입니다.

    #endregion

    #region 이벤트 액션

    public event Action OnBindingsChanged; // 키 변경·복원 후 표시 갱신을 요청합니다.

    #endregion

    #region 프로퍼티

    public InputActionAsset InputActions => _inputActions; // 키 설정 페이지에서 사용할 실제 입력 에셋입니다.
    public float MusicVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Music", 1f)); // 음악 볼륨의 저장값입니다.
    public float EffectsVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Effects", 1f)); // 효과음 볼륨의 저장값입니다.
    public bool FullScreen => PlayerPrefs.GetInt(Prefix + "FullScreen", Screen.fullScreen ? 1 : 0) == 1; // 현재 선택한 화면 모드입니다.

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 설정 페이지가 닫혀 있어도 공통 Canvas 생성 시 저장된 키를 불러옵니다.
    /// </summary>
    void Awake()
    {
        if (_inputActions != null)
        {
            string json = PlayerPrefs.GetString(Prefix + "Bindings", string.Empty); // 저장 JSON 값입니다.
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    _inputActions.LoadBindingOverridesFromJson(json);
                }
                catch (ArgumentException)
                {
                    Debug.LogWarning("저장된 키 설정을 불러오지 못했습니다.", this);
                }
            }
        }
    }

    /// <summary>
    /// 오디오 초기화 이후 믹서 볼륨을 적용하고 저장된 화면 모드가 있으면 복원합니다.
    /// </summary>
    void Start()
    {
        ApplyVolumes();
        if (PlayerPrefs.HasKey(Prefix + "FullScreen"))
        {
            ApplyScreenMode(FullScreen);
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 0~1 볼륨을 믹서의 데시벨 값으로 변환합니다. 0은 음소거 수준입니다.
    /// </summary>
    /// <param name="volume">볼륨 값입니다.</param>
    public static float ToDecibels(float volume)
    {
        return volume <= 0f ? -80f : Mathf.Log10(Mathf.Clamp01(volume)) * 20f;
    }

    /// <summary>
    /// 음악과 효과음 볼륨을 서로 독립된 믹서 채널에 적용합니다.
    /// </summary>
    public void ApplyVolumes()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_audioMixer == null)
        {
            return;
        }

        _audioMixer.SetFloat(_musicParameter, ToDecibels(MusicVolume));
        _audioMixer.SetFloat(_effectsParameter, ToDecibels(EffectsVolume));
    }

    /// <summary>
    /// 음악 슬라이더 값을 저장하고 음악 채널 볼륨을 변경합니다.
    /// </summary>
    /// <param name="value">현재 표시 항목에 적용할 값입니다.</param>
    public void SetMusicVolume(float value)
    {
        PlayerPrefs.SetFloat(Prefix + "Music", Mathf.Clamp01(value));
        ApplyVolumes();
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 효과음 슬라이더 값을 저장하고 효과음 채널 볼륨을 변경합니다.
    /// </summary>
    /// <param name="value">현재 표시 항목에 적용할 값입니다.</param>
    public void SetEffectsVolume(float value)
    {
        PlayerPrefs.SetFloat(Prefix + "Effects", Mathf.Clamp01(value));
        ApplyVolumes();
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 화면 모드 선택을 저장하고 적용합니다.
    /// </summary>
    /// <param name="fullScreen">전체 화면 여부입니다.</param>
    public void SetFullScreen(bool fullScreen)
    {
        PlayerPrefs.SetInt(Prefix + "FullScreen", fullScreen ? 1 : 0);
        ApplyScreenMode(fullScreen);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 현재 모니터 해상도를 기준으로 전체 화면은 원본 크기, 창 모드는 가로·세로의 절반을 사용합니다.
    /// </summary>
    /// <param name="fullScreen">전체 화면 여부입니다.</param>
    void ApplyScreenMode(bool fullScreen)
    {
        var display = Screen.mainWindowDisplayInfo;
        int width = display.width > 0 ? display.width : Screen.currentResolution.width;
        int height = display.height > 0 ? display.height : Screen.currentResolution.height;

        if (fullScreen)
        {
            Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
        }
        else
        {
            Screen.SetResolution(Mathf.Max(1, width / 2), Mathf.Max(1, height / 2), FullScreenMode.Windowed);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 원본 입력 에셋을 저장하지 않고 실행 중 바인딩 변경만 사용자 설정으로 저장합니다.
    /// </summary>
    public void SaveBindings()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_inputActions == null)
        {
            return;
        }

        string json = _inputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(Prefix + "Bindings", json);
        GameSaveService.UpdateActiveKeyBindings(json);
        PlayerPrefs.Save();
        OnBindingsChanged?.Invoke();
    }

    /// <summary>
    /// 현재 키 배치를 세이브 슬롯에 보관할 JSON으로 반환합니다.
    /// </summary>
    public static string CaptureKeyBindings()
    {
        var asset = InputSystem.actions;
        return asset != null ? asset.SaveBindingOverridesAsJson() : PlayerPrefs.GetString(Prefix + "Bindings", string.Empty);
    }

    /// <summary>세이브 슬롯의 키 배치를 적용합니다. 키 정보가 없는 기존 저장은 유지합니다.</summary>
    public static void RestoreKeyBindings(string json)
    {
        if (json == null) return;
        var views = FindObjectsByType<SettingPreferencesView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var assets = new System.Collections.Generic.HashSet<InputActionAsset>();
        if (InputSystem.actions != null) assets.Add(InputSystem.actions);
        foreach (var view in views) if (view._inputActions != null) assets.Add(view._inputActions);
        try
        {
            foreach (var asset in assets)
            {
                if (string.IsNullOrEmpty(json)) asset.RemoveAllBindingOverrides();
                else asset.LoadBindingOverridesFromJson(json);
            }
        }
        catch (ArgumentException)
        {
            Debug.LogWarning("저장된 키 설정을 복원하지 못했습니다.");
            return;
        }
        PlayerPrefs.SetString(Prefix + "Bindings", json);
        PlayerPrefs.Save();
        foreach (var view in views) view.OnBindingsChanged?.Invoke();
    }

    /// <summary>사용자 키 변경을 해제하고 기본 키 배치로 되돌립니다.</summary>
    public void ResetBindings()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_inputActions == null)
        {
            return;
        }

        _inputActions.RemoveAllBindingOverrides();
        SaveBindings();
    }

    #endregion

}
