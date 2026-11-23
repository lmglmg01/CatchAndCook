using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Board Variant의 통합 설정 화면 표시와 연출만 담당합니다.
/// </summary>
[DefaultExecutionOrder(1200)]
public class SettingScreenView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] SettingView _settings; // 설정 표시를 연결할 뷰입니다.
    [SerializeField] CanvasGroup _screenGroup; // 화면 그룹 값입니다.
    [SerializeField] CanvasGroup[] _hideGroups; // 숨김 그룹 목록입니다.
    // _windowMode: 창 모드 이미지입니다.
    // _fullScreen: 전체 화면 이미지입니다.
    [SerializeField] UnityEngine.UI.Image _windowMode, _fullScreen;
    [SerializeField] Sprite _normalModeSprite, _selectedModeSprite; // 화면 모드 선택 버튼의 미선택/선택 배경 에셋을 연결합니다.
    [SerializeField] Material _lightIconMaterial; // 어두운 배경에서 아이콘을 밝게 표시할 UIIconTint 재질을 연결합니다. Image.color는 흰색으로 유지합니다.
    [SerializeField] UnityEngine.UI.Image[] _sectionIcons; // 화면·소리·키보드 설정의 제목 아이콘을 순서대로 연결합니다.

    #endregion

    #region 런타임 상태

    readonly Dictionary<CanvasGroup, Vector3> _hidden = new Dictionary<CanvasGroup, Vector3>(); // 숨김에 사용하는 대상별 매핑입니다.
    Tween _openTween; // 열기 트윈 값입니다.

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 뷰가 켜질 때 표시와 이벤트 연결을 갱신합니다.
    /// </summary>
    void OnEnable()
    {
        // group: 그룹 값입니다.
        foreach (var group in _hideGroups)
        {
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (group == null)
            {
                continue;
            }

            _hidden[group] = new Vector3(group.alpha, group.interactable ? 1 : 0, group.blocksRaycasts ? 1 : 0);
            group.alpha = 0;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        // DOTween 연출: 설정 화면을 시간 배율과 무관하게 페이드 인합니다.
        if (Application.isPlaying && GetComponentInParent<AppFadeView>() == null)
        {
            _screenGroup.alpha = 0;
            _openTween = _screenGroup.DOFade(1, .2f).SetUpdate(true);
        }

        RefreshPresentation();
    }

    /// <summary>
    /// 다른 갱신이 끝난 뒤 화면 표시를 맞춥니다.
    /// </summary>
    void LateUpdate()
    {
        // group: 그룹 값입니다.
        foreach (var group in _hidden.Keys)
        {
            if (group != null)
            {
                group.alpha = 0;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }
    }

    /// <summary>
    /// 뷰가 꺼질 때 이벤트와 진행 중인 표시를 정리합니다.
    /// </summary>
    void OnDisable()
    {
        // DOTween 연출 정리: 이 View가 만든 Tween만 종료하고 기존 HUD 상태를 복원합니다.
        _openTween?.Kill();
        if (_screenGroup != null)
        {
            _screenGroup.alpha = 1;
        }

        // pair: pair 값입니다.
        foreach (var pair in _hidden)
        {
            if (pair.Key != null)
            {
                pair.Key.alpha = pair.Value.x;
                pair.Key.interactable = pair.Value.y > .5f;
                pair.Key.blocksRaycasts = pair.Value.z > .5f;
            }
        }

        _hidden.Clear();
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 화면 모드와 설정 항목의 선택 표시를 갱신합니다.
    /// </summary>
    public void RefreshPresentation()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_settings == null)
        {
            return;
        }

        var navy = new Color(0f, 54f / 255f, 85f / 255f); /* 구상도 남색 #003655입니다. */
        var cream = new Color(252f / 255f, 241f / 255f, 234f / 255f); // 밝은 선택 배경 원본과 같은 #FCF1EA입니다.
        if (_windowMode != null)
        {
            _windowMode.color = _settings.DisplayFullScreen ? navy : cream;
            // t: 대상 값입니다.
            foreach (var t in _windowMode.GetComponentsInChildren<TMP_Text>(true))
            {
                t.color = _settings.DisplayFullScreen ? new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f) : navy;
            }
        }

        if (_fullScreen != null)
        {
            _fullScreen.color = _settings.DisplayFullScreen ? cream : navy;
            // t: 대상 값입니다.
            foreach (var t in _fullScreen.GetComponentsInChildren<TMP_Text>(true))
            {
                t.color = _settings.DisplayFullScreen ? navy : new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f);
            }
        }

        ApplyModeArt(_windowMode, !_settings.DisplayFullScreen);
        ApplyModeArt(_fullScreen, _settings.DisplayFullScreen);
        ApplyModeIcon(_windowMode, !_settings.DisplayFullScreen);
        ApplyModeIcon(_fullScreen, _settings.DisplayFullScreen);
        if (_sectionIcons != null)
        {
            // icon: 아이콘 값입니다.
            foreach (var icon in _sectionIcons)
            {
                if (icon != null)
                {
                    icon.color = Color.white;
                    icon.material = _lightIconMaterial;
                }
            }
        }
    }

    /// <summary>
    /// 선택 배경 에셋의 원래 색을 유지하면서 모드 버튼을 표시합니다.
    /// </summary>
    /// <param name="image">이미지입니다.</param>
    /// <param name="selected">선택 여부입니다.</param>
    void ApplyModeArt(UnityEngine.UI.Image image, bool selected)
    {
        var sprite = selected ? _selectedModeSprite : _normalModeSprite; // 스프라이트 값입니다.
        if (image != null && sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
        }
    }

    /// <summary>
    /// 흰색 Color를 유지하고 어두운 배경에서만 밝은 표시 재질을 사용합니다.
    /// </summary>
    /// <param name="image">이미지입니다.</param>
    /// <param name="selected">선택 여부입니다.</param>
    void ApplyModeIcon(UnityEngine.UI.Image image, bool selected)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (image == null)
        {
            return;
        }

        var icon = image.transform.Find("ModeIcon"); // 아이콘 값입니다.
        if (icon != null && icon.TryGetComponent<UnityEngine.UI.Image>(out var graphic))
        {
            graphic.color = Color.white;
            graphic.material = selected ? null : _lightIconMaterial;
        }
    }

    #endregion

}
