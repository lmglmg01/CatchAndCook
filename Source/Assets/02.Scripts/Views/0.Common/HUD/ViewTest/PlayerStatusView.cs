using UnityEngine;
using TMPro;

// 기본 HUD의 플레이어 초상화, 이름, 레벨과 경험치를 표시합니다.
public class PlayerStatusView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [Header("플레이어 프로필")]
    [SerializeField] UnityEngine.UI.Image _portrait; // 초상화 이미지입니다.
    [SerializeField] TMP_Text _playerName; // 플레이어 이름 표시 텍스트입니다.
    [SerializeField] TMP_Text _level; // 레벨 표시 텍스트입니다.
    [SerializeField] UnityEngine.UI.Image _progress; // 진행도 이미지입니다.

    #endregion

    #endregion

    Hero _portraitHero;
    TalkPortraitPreview _preview;
    float _nextPreviewAttempt;

    internal void BindCharacter(Hero hero)
    {
        ReleasePreview();
        _portraitHero = hero;
        _nextPreviewAttempt = 0f;
    }

    void LateUpdate()
    {
        if (_portraitHero == null || _portrait == null) return;
        var canvas = _portrait.canvas;
        bool visible = _portrait.isActiveAndEnabled && canvas != null && canvas.rootCanvas.isActiveAndEnabled
            && _portrait.canvasRenderer.GetInheritedAlpha() > 0f;
        if (_preview == null)
        {
            if (!visible || Time.unscaledTime < _nextPreviewAttempt) return;
            _nextPreviewAttempt = Time.unscaledTime + 1f;
            _preview = TalkPortraitPreview.Create(_portrait, _portraitHero, forHud: true);
            if (_preview == null) return;
            if (!_portrait.TryGetComponent<UnityEngine.UI.Mask>(out _))
                _portrait.gameObject.AddComponent<UnityEngine.UI.Mask>();
        }
        _preview.UpdateHud(visible);
    }

    void OnDisable()
    {
        ReleasePreview();
        _nextPreviewAttempt = 0f;
    }

    void ReleasePreview()
    {
        _preview?.Dispose();
        _preview = null;
    }

    #region UI 표시 및 갱신

    /// <summary>
    /// 플레이어 초상화 이미지를 변경합니다.
    /// </summary>
    /// <param name="portrait">초상화 이미지입니다.</param>
    public void SetPortrait(Sprite portrait)
    {
        if (_portrait != null)
        {
            _portrait.sprite = portrait;
        }
    }

    /// <summary>
    /// 플레이어 레벨을 텍스트에 표시합니다.
    /// </summary>
    /// <param name="value">현재 표시 항목에 적용할 값입니다.</param>
    public void SetLevel(int value)
    {
        if (_level != null)
        {
            _level.text = "Lv." + value;
        }
    }

    /// <summary>
    /// 플레이어 진행도 게이지를 갱신합니다.
    /// </summary>
    /// <param name="value">현재 표시 항목에 적용할 값입니다.</param>
    public void SetProgress(float value)
    {
        if (_progress != null)
        {
            _progress.fillAmount = Mathf.Clamp01(value);
        }
    }

    #endregion

    #region 표시 연출 및 정리

    /// <summary>
    /// 플레이어 이름 텍스트를 갱신합니다.
    /// </summary>
    /// <param name="value">현재 표시 항목에 적용할 값입니다.</param>
    public void SetPlayerName(string value)
    {
        if (_playerName != null)
        {
            _playerName.text = value;
        }
    }

    #endregion

}
