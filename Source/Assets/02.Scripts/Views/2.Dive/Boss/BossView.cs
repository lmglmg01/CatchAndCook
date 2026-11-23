using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 보스 이름과 HP 표시, 조우 알림 및 체력 연출을 담당하는 뷰입니다.
/// </summary>
public partial class BossView : MonoBehaviour
{
    // 에디터 전용 영역은 기존 미리보기 테스트를 유지하며 실제 게임 빌드에서는 제외합니다.

    #region 멤버

    #region UI 연결 및 표시 설정

    public bool IsEncounterPlaying => _noticing;

    [Header("알림")]
    [SerializeField] GameObject _bossNotice; // 보스 조우 알림 오브젝트입니다.
    [SerializeField] UnityEngine.UI.Image _noticeBackground; // 보스 조우 알림의 배경 이미지입니다.

    [Header("체력")]
    [SerializeField] GameObject _bossBoard; // 보스 이름과 HP를 표시할 체력 창입니다.
    [SerializeField] TMP_Text _nameText; // 체력 창의 보스 이름입니다.
    [SerializeField] TMP_Text _hpText; // 현재 HP와 최대 HP를 표시할 텍스트입니다.
    [SerializeField] UnityEngine.UI.Image _HpBarBackImage; // 지연 후 감소하는 뒤 HP 바입니다.
    [SerializeField] UnityEngine.UI.Image _HpBarForwardImage; // 즉시 감소하며 깜빡이는 앞 HP 바입니다.

    [Header("DOTween 연출 연결")]
    [SerializeField] CanvasGroup _boardGroup; // BossBoard에 연결할 사망 페이드용 CanvasGroup입니다.
    [SerializeField] Color _hpFlashColor = Color.red; // 앞 HP 바가 피격 시 잠깐 표시할 색상입니다.

    [Header("조우 · 사망 연출 설정")]
    [SerializeField, Min(0f), Tooltip("보스 조우 연출 전체 시간(초)입니다. 첫 1초 뒤 화살표가 등장하고 남은 구간에 안내판·아이콘 등장과 퇴장을 배분합니다. 0이면 생략합니다.")]
    float _bossNoticeDuration = 3f; // 조우 알림 전체 지속시간입니다.
    [SerializeField, Min(0f), Tooltip("사망 시 흔들림이 끝난 후 위로 이동하며 사라지는 시간(초)입니다. 0이면 흔들림 종료 후 즉시 숨깁니다.")]
    float _deathFadeDuration = 1f; // 사망 시 상승과 페이드아웃 지속시간입니다.
    [SerializeField, Min(0f)] float _deathShakeDuration = 1f; // 뒤 HP 바가 0이 된 뒤 좌우로 흔들리는 시간입니다.
    [SerializeField, Min(0f)] float _deathShakeStrength = 8f; // 좌우 흔들림의 크기입니다.
    [SerializeField, Min(0f)] float _deathRiseDistance = 200f; // 사망 시 위로 이동할 거리입니다.

    [Header("HP 갱신 연출 설정")]
    [SerializeField, Min(0f), Tooltip("앞 HP 바가 지정 횟수만큼 깜빡이는 전체 시간(초)입니다. 0이면 깜빡임을 생략합니다.")]
    float _hpFlashDuration = .2f; // 앞 HP 바의 전체 깜빡임 시간입니다.
    [SerializeField, Min(0), Tooltip("빨간색으로 변했다가 원래 색으로 돌아오는 왕복 횟수입니다. 0이면 깜빡임을 생략합니다.")]
    int _hpFlashCount = 4; // 앞 HP 바의 깜빡임 왕복 횟수입니다.
    [SerializeField, Min(0f), Tooltip("뒤 HP 바가 기존 HP 표시를 유지하는 대기 시간(초)입니다.")]
    float _backHpDelay = 1f; // 뒤 HP 바가 감소하기 전 대기 시간입니다.
    [SerializeField, Min(0f), Tooltip("대기 후 뒤 HP 바가 목표 HP까지 줄어드는 지속시간(초)입니다.")]
    float _backHpDuration = .3f; // 뒤 HP 바가 목표 HP까지 감소하는 시간입니다.

    [Header("체력 아이콘 피격 흔들림")]
    [SerializeField, Min(0f)] float _iconHitDuration = .75f;
    [SerializeField, Range(0f, 45f)] float _iconHitAngle = 18f;
    [SerializeField, Range(1f, 2f)] float _iconHitPeakScale = 1.3f;

    #endregion

    #region 런타임 상태

    Sequence _noticeTween; // 조우 알림 연출입니다.
    Sequence _flashTween; // 앞 HP 바의 깜빡임 연출입니다.
    Tween _backHpTween; // 뒤 HP 바의 지연 감소 연출입니다.
    Tween _iconHitTween;
    RectTransform _healthIcon;
    Vector2 _healthIconPosition;
    Quaternion _healthIconRotation;
    Vector3 _healthIconScale;
    Tween _deathTween; // 체력 창의 사망 페이드 연출입니다.
    float _backHpDelayUntil; // 연속 피해에도 다시 미루지 않는 뒤 HP 바의 최초 감소 시작 시점입니다.
    Vector2 _boardPosition; // 재조우와 초기화 시 복원할 체력 창의 원래 위치입니다.
    Color _noticeColor; // 조우 배경의 원래 색상입니다.
    Color _forwardColor; // 앞 HP 바의 원래 색상입니다.
    float _currentHp; // 현재 표시 중인 HP입니다.
    float _maxHp; // 현재 표시 중인 최대 HP입니다.
    bool _cached; // 원래 위치와 색상을 보관했는지 여부입니다.
    bool _hasBoss; // 표시할 보스 데이터를 받았는지 여부입니다.
    bool _noticing; // 조우 알림이 재생 중인지 여부입니다.
    bool _dying; // 사망 페이드가 진행 중인지 여부입니다.

    #endregion

#if UNITY_EDITOR
    #region 임시 데이터 및 테스트 멤버

    [Header("임시 연출 테스트 · 실제 보스 데이터와 무관")]
    [SerializeField] string _testBossName = "자이언트 장어"; // 테스트용 보스 이름입니다.
    [SerializeField, Min(1f)] float _testMaxHp = 1000f; // 테스트용 최대 HP입니다.
    [SerializeField, Min(0f)] float _testDamage = 250f; // 일부 피해 테스트에서 차감할 HP입니다.
    [SerializeField, Min(1)] int _testRepeatedDamageCount = 10; // 연속 피해 테스트의 타격 횟수입니다.
    [SerializeField, Min(.01f)] float _testRepeatedDamageInterval = .15f; // 연속 타격 사이의 실제 시간 간격입니다.
    [SerializeField, Min(0f)] float _testRepeatedDamage = 50f; // 연속 피해 테스트의 1회 피해량입니다.
    Coroutine _testDamageRoutine; // ContextMenu가 닫힌 뒤 정상 프레임에 피해 테스트를 시작합니다.

    #endregion
#endif

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 뷰가 숨겨지면 실행 중 연출과 테스트를 종료하고 표시 상태를 정리합니다.
    /// </summary>
    void OnDisable()
    {
        HideBoss();
    }

    /// <summary>
    /// 뷰가 제거되면 이 View에서 생성한 트윈과 대기 중인 테스트를 정리합니다.
    /// </summary>
    void OnDestroy()
    {
        StopPresentationTweens();
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 보스 조우를 표시합니다. 최대 HP는 양수여야 하며 현재 HP는 0~최대값으로 제한합니다.
    /// </summary>
    /// <param name="bossName">표시할 보스 이름입니다.</param>
    /// <param name="currentHp">외부에서 전달한 현재 HP입니다.</param>
    /// <param name="maxHp">외부에서 전달한 최대 HP입니다.</param>
    public void ShowBoss(string bossName, float currentHp, float maxHp)
    {
        if (!ValidHp(currentHp, maxHp) || !CachePresentation())
        {
            return;
        }

        StopPresentationTweens();
        gameObject.SetActive(true);
        RestorePresentation();
        _hasBoss = true;
        _currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
        _maxHp = maxHp;
        string displayName = string.IsNullOrWhiteSpace(bossName) ? "보스" : bossName; // 빈 이름이면 기본 보스 이름을 표시합니다.
        _nameText.text = displayName;
        RefreshHpText();
        SetBarRatio(_HpBarForwardImage, _currentHp / _maxHp);
        SetBarRatio(_HpBarBackImage, _currentHp / _maxHp);
        _bossNotice.SetActive(false);
        _bossBoard.SetActive(false);
        if (_currentHp <= 0f)
        {
            _bossBoard.SetActive(true);
            PlayDeath();
            return;
        }

        PlayEncounter();
    }

    /// <summary>
    /// 현재 HP와 최대 HP를 받아 표시합니다. 조우 중에는 마지막 HP를 보관해 알림 종료 시 적용합니다.
    /// </summary>
    /// <param name="currentHp">외부에서 전달한 현재 HP입니다.</param>
    /// <param name="maxHp">외부에서 전달한 최대 HP입니다.</param>
    public void UpdateHp(float currentHp, float maxHp)
    {
        if (!_hasBoss || _dying || !ValidHp(currentHp, maxHp))
        {
            return;
        }

        float next = Mathf.Clamp(currentHp, 0f, maxHp); // 표시 가능한 범위로 제한한 다음 HP입니다.
        if (_currentHp == next && _maxHp == maxHp)
        {
            return;
        }

        bool damaged = next < _currentHp;
        _currentHp = next;
        _maxHp = maxHp;
        RefreshHpText();
        if (_noticing)
        {
            SetBarRatio(_HpBarForwardImage, _currentHp / _maxHp);
            SetBarRatio(_HpBarBackImage, _currentHp / _maxHp);
            if (_currentHp <= 0f)
            {
                // 알림 도중 사망하면 대기 중인 조우 완료 콜백을 취소해 HP 창 재등장을 막습니다.
                _noticeTween?.Kill();
                _noticeTween = null;
                RestoreEncounterUI();
                _noticing = false;
                _bossNotice.SetActive(false);
                _hasBoss = false;
            }
            return;
        }

        if (damaged) PlayHealthIconHit();
        PlayHpChange();
        if (_currentHp <= 0f)
        {
            PlayDeath();
        }
    }

    /// <summary>
    /// 기존 최대 HP를 유지하면서 현재 HP만 갱신합니다.
    /// </summary>
    /// <param name="currentHp">외부에서 전달한 현재 HP입니다.</param>
    public void UpdateHp(float currentHp)
    {
        UpdateHp(currentHp, _maxHp);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 외부에서 전달한 피해량으로 표시 HP를 감소시키고 피해·사망 연출을 실행합니다.
    /// 실제 보스 데이터는 변경하지 않습니다. 차감 후 실제 HP를 전달할 때는 UpdateHp를 사용합니다.
    /// </summary>
    /// <param name="damage">실제 전투에서 확정된 양수의 피해량입니다.</param>
    public void ApplyDamage(float damage)
    {
        if (!_hasBoss || _dying)
        {
            return;
        }

        if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
        {
            return;
        }

        // 실제 HP 갱신과 같은 경로를 사용해 앞 HP 즉시 반영·뒤 HP 지연·사망 연출을 유지합니다.
        UpdateHp(_currentHp - damage, _maxHp);
    }
#endif

    /// <summary>
    /// 보스 표시를 즉시 닫고 이 View에서 만든 트윈과 임시 표시 상태를 정리합니다.
    /// </summary>
    public void HideBoss()
    {
        StopPresentationTweens();

        _hasBoss = false;
        _noticing = false;
        _dying = false;
        if (_bossNotice != null)
        {
            _bossNotice.SetActive(false);
        }

        if (_bossBoard != null)
        {
            _bossBoard.SetActive(false);
        }

        RestorePresentation();
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 연출 전 인스펙터 연결을 검증하고 원래 위치·색상을 한 번 보관합니다.
    /// </summary>
    bool CachePresentation()
    {
        if (_cached)
        {
            return true;
        }

        if (_boardGroup == null && _bossBoard != null)
        {
            _boardGroup = _bossBoard.GetComponent<CanvasGroup>();
        }

        if (_bossNotice == null || _noticeBackground == null
            || _bossBoard == null || _nameText == null || _hpText == null
            || _HpBarBackImage == null || _HpBarForwardImage == null || _boardGroup == null)
        {
            Debug.LogWarning("BossView: 알림·체력 UI 참조와 BossBoard의 CanvasGroup을 연결해 주세요.", this);
            return false;
        }

        _boardPosition = ((RectTransform)_bossBoard.transform).anchoredPosition;
        _noticeColor = _noticeBackground.color;
        _forwardColor = _HpBarForwardImage.color;
        _cached = true;
        return true;
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    /// <summary>
    /// 입력 오류로 기존 표시 상태가 손상되지 않도록 유효한 HP만 허용합니다.
    /// </summary>
    /// <param name="currentHp">외부에서 전달한 현재 HP입니다.</param>
    /// <param name="maxHp">외부에서 전달한 최대 HP입니다.</param>
    bool ValidHp(float currentHp, float maxHp)
    {
        // 현재 HP와 최대 HP의 유효 여부입니다.
        bool valid = !float.IsNaN(currentHp) && !float.IsInfinity(currentHp)
            && !float.IsNaN(maxHp) && !float.IsInfinity(maxHp) && maxHp > 0f;
        if (!valid)
        {
            Debug.LogWarning("BossView: 현재 HP는 유한한 값, 최대 HP는 0보다 큰 유한한 값이어야 합니다.", this);
        }

        return valid;
    }

    /// <summary>
    /// HP 숫자는 현재값 / 최대값으로 표시합니다.
    /// </summary>
    void RefreshHpText()
    {
        _hpText.text = $"{_currentHp:0.##}/{_maxHp:0.##}";
    }

    /// <summary>
    /// Sliced 바는 너비로 HP를 표시해 둥근 끝 모양을 보존하고, 기존 Filled 바도 지원합니다.
    /// </summary>
    /// <param name="image">HP를 표시할 이미지입니다.</param>
    /// <param name="ratio">표시할 HP 비율입니다.</param>
    void SetBarRatio(UnityEngine.UI.Image image, float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        image.fillAmount = ratio; // 기존 표시 비율과 인스펙터 값은 유지합니다.
        if (image.type != UnityEngine.UI.Image.Type.Sliced)
        {
            return;
        }

        var rect = image.rectTransform; // HP 바의 너비를 갱신할 사각 영역입니다.
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(ratio, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 뒤 HP 바의 현재 표시 비율을 읽어 연속 피해에도 현재 위치부터 이어서 움직입니다.
    /// </summary>
    /// <param name="image">HP를 표시할 이미지입니다.</param>
    float BarRatio(UnityEngine.UI.Image image)
    {
        return image.fillAmount;
    }

    #endregion

    #region 표시 연출 및 정리

    /// <summary>
    /// 비네트 경고·화살표·안내판과 아이콘 등장·동시 퇴장을 조우 대기 시간 안에 재생합니다.
    /// </summary>
    void PlayEncounter()
    {
        _noticing = true;
        _bossBoard.SetActive(false);
        float duration = Mathf.Max(0f, _bossNoticeDuration);
        if (duration <= 0f)
        {
            _noticing = false;
            _bossNotice.SetActive(false);
            _boardGroup.alpha = 1f;
            _bossBoard.SetActive(_hasBoss && _currentHp > 0f);
            return;
        }

        _bossNotice.SetActive(true);
        HideOtherEncounterUI();
        SampleEncounterPresentation(0f, duration);
        _noticeTween = DOTween.Sequence().SetUpdate(true);
        _noticeTween.Append(DOTween.To(() => 0f, elapsed => SampleEncounterPresentation(elapsed, duration), duration, duration).SetEase(Ease.Linear));
        _noticeTween.OnComplete(() =>
        {
            _noticeTween = null;
            RestoreEncounterUI();
            _noticing = false;
            _bossNotice.SetActive(false);
            if (!_hasBoss || _currentHp <= 0f) return;
            _boardGroup.alpha = 1f;
            _bossBoard.SetActive(true);
        });
    }

    /// <summary>기존 아이콘 전체를 제자리에서 확대·감쇠 회전한 뒤 원래 자세로 복구합니다.</summary>
    void PlayHealthIconHit()
    {
        if (_healthIcon == null)
        {
            _healthIcon = _bossBoard.transform.Find("BossHealthIcon") as RectTransform;
            if (_healthIcon == null) return;
            _healthIconPosition = _healthIcon.anchoredPosition;
            _healthIconRotation = _healthIcon.localRotation;
            _healthIconScale = _healthIcon.localScale;
        }

        float startAngle = Mathf.DeltaAngle(0f, (Quaternion.Inverse(_healthIconRotation) * _healthIcon.localRotation).eulerAngles.z);
        Vector3 startScale = _healthIcon.localScale;
        _iconHitTween?.Kill();
        _iconHitTween = null;
        float duration = Mathf.Max(0f, _iconHitDuration);
        float strength = Mathf.Max(0f, _iconHitAngle);
        float peak = Mathf.Max(1f, _iconHitPeakScale);
        if (duration <= 0f)
        {
            RestoreHealthIconPose();
            return;
        }

        _iconHitTween = DOTween.To(() => 0f, progress =>
        {
            if (_healthIcon == null) return;
            float remaining = 1f - progress;
            float wave = Mathf.Sin(progress * Mathf.PI * 12f) * strength * remaining * remaining;
            float angle = Mathf.Clamp(startAngle * remaining * remaining * remaining + wave, -strength, strength);
            _healthIcon.localRotation = _healthIconRotation * Quaternion.AngleAxis(angle, Vector3.forward);
            if (progress < .1f)
            {
                float grow = 1f - Mathf.Pow(1f - progress / .1f, 3f);
                _healthIcon.localScale = Vector3.LerpUnclamped(startScale, _healthIconScale * peak, grow);
            }
            else
            {
                float shrink = Mathf.Clamp01((progress - .1f) / .9f);
                _healthIcon.localScale = _healthIconScale * (1f + (peak - 1f) * (1f - shrink) * (1f - shrink));
            }
        }, 1f, duration).SetEase(Ease.Linear).SetUpdate(true).SetTarget(_healthIcon).OnComplete(() =>
        {
            _iconHitTween = null;
            RestoreHealthIconPose();
        });
    }

    void RestoreHealthIconPose()
    {
        if (_healthIcon == null) return;
        _healthIcon.anchoredPosition = _healthIconPosition;
        _healthIcon.localRotation = _healthIconRotation;
        _healthIcon.localScale = _healthIconScale;
    }

    void StopHealthIconHit()
    {
        _iconHitTween?.Kill();
        _iconHitTween = null;
        RestoreHealthIconPose();
    }

    /// <summary>
    /// 앞 HP는 즉시 적용하고 뒤 HP는 지연 후 현재 표시값부터 목표 비율로 이동합니다.
    /// </summary>
    void PlayHpChange()
    {
        float ratio = _currentHp / _maxHp; // 현재 HP를 최대 HP로 나눈 표시 비율입니다.
        SetBarRatio(_HpBarForwardImage, ratio);
        bool flashing = _flashTween != null && _flashTween.IsActive(); // 연속 피해 시 현재 깜빡임 색상을 이어갑니다.
        _flashTween?.Kill();
        if (!flashing)
        {
            _HpBarForwardImage.color = _forwardColor;
        }
        _flashTween = null;
        int count = Mathf.Max(0, _hpFlashCount); // 음수를 제외한 깜빡임 횟수입니다.
        float flashDuration = Mathf.Max(0f, _hpFlashDuration); // 음수를 제외한 전체 깜빡임 시간입니다.
        // DOTween 연출: 총시간을 (횟수 × 왕복 2구간)으로 나누어 재생하고 원래 색으로 복원합니다. 0 설정은 생략합니다.
        if (count > 0 && flashDuration > 0f)
        {
            float halfCycle = flashDuration / count / 2f; // 깜빡임 왕복 중 한쪽 색상 전환 시간입니다.
            _flashTween = DOTween.Sequence().SetUpdate(true);
            _flashTween.Append(_HpBarForwardImage.DOColor(_hpFlashColor, halfCycle).SetEase(Ease.Linear));
            _flashTween.Append(_HpBarForwardImage.DOColor(_forwardColor, halfCycle).SetEase(Ease.Linear));
            _flashTween.SetLoops(count).OnComplete(() =>
            {
                _flashTween = null;
                _HpBarForwardImage.color = _forwardColor;
            });
        }

        float delay = Mathf.Max(0f, _backHpDelay); // 첫 피해에만 전체 대기 시간을 적용합니다.
        if (_backHpTween != null && _backHpTween.IsActive())
        {
            delay = Mathf.Max(0f, _backHpDelayUntil - Time.unscaledTime);
        }
        else
        {
            _backHpDelayUntil = Time.unscaledTime + delay;
        }

        _backHpTween?.Kill();
        // DOTween 연출: 연속 피해는 최초 대기 종료 시점을 유지하고 현재 너비부터 최신 HP로 이어갑니다.
        // 이미 감소 중이면 추가 대기 없이 목표를 갱신해 피격마다 멈추거나 너비가 튀지 않도록 합니다.
        _backHpTween = DOTween.To(() => BarRatio(_HpBarBackImage), value => SetBarRatio(_HpBarBackImage, value), ratio, Mathf.Max(0f, _backHpDuration))
            .SetDelay(delay)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _backHpTween = null;
                if (_dying && _currentHp <= 0f)
                {
                    PlayDeath();
                }
            });
    }

    /// <summary>
    /// 뒤 HP 바가 0이 될 때까지 기다린 뒤 좌우 흔들림과 상승·퇴장 연출을 실행합니다.
    /// </summary>
    void PlayDeath()
    {
        _dying = true;
        if (_backHpTween != null && _backHpTween.IsActive())
        {
            // DOTween 연출: 지연 감소가 끝나면 뒤 HP 바의 완료 콜백에서 이 함수를 다시 호출합니다.
            return;
        }

        if (_deathTween != null && _deathTween.IsActive())
        {
            return;
        }

        var board = (RectTransform)_bossBoard.transform; // 흔들림과 상승을 적용할 체력 창입니다.
        float duration = Mathf.Max(0f, _deathFadeDuration); // 상승 및 페이드아웃 지속시간입니다.
        var sequence = DOTween.Sequence().SetUpdate(true); // 대기 이후의 사망 연출을 순서대로 재생합니다.
        _deathTween = sequence;
        float shakeDuration = Mathf.Max(0f, _deathShakeDuration); // 좌우 흔들림 지속시간입니다.
        if (shakeDuration > 0f)
        {
            // DOTween 연출: HP가 완전히 0이 된 뒤 기본 1초간 가로축으로만 흔들립니다.
            sequence.Append(board.DOShakeAnchorPos(shakeDuration, new Vector2(Mathf.Max(0f, _deathShakeStrength), 0f), 20, 0f, false, true));
        }

        sequence.AppendCallback(() =>
        {
            board.anchoredPosition = _boardPosition;
        });
        if (duration > 0f)
        {
            // DOTween 연출: 흔들림이 끝나면 InOutElastic으로 위로 올라가며 동시에 투명해집니다.
            sequence.Append(board.DOAnchorPosY(_boardPosition.y + Mathf.Max(0f, _deathRiseDistance), duration).SetEase(Ease.InOutElastic));
            sequence.Join(_boardGroup.DOFade(0f, duration).SetEase(Ease.Linear));
        }

        sequence.OnComplete(() =>
        {
            _deathTween = null;
            _boardGroup.alpha = 0f;
            _bossBoard.SetActive(false);
            _hasBoss = false;
        });
    }

    /// <summary>
    /// 이 View에서 생성한 트윈만 정리합니다. 다른 UI 트윈은 종료하지 않습니다.
    /// </summary>
    void StopPresentationTweens()
    {
#if UNITY_EDITOR
        if (_testDamageRoutine != null)
        {
            StopCoroutine(_testDamageRoutine);
            _testDamageRoutine = null;
        }
#endif

        StopHealthIconHit();
        _noticeTween?.Kill();
        _noticeTween = null;
        RestoreEncounterUI();
        _flashTween?.Kill();
        _flashTween = null;
        _backHpTween?.Kill();
        _backHpTween = null;
        _deathTween?.Kill();
        _deathTween = null;
        _noticing = false;
        _dying = false;
    }

    /// <summary>
    /// 재조우 시 시작 위치와 색상을 복구합니다.
    /// </summary>
    void RestorePresentation()
    {
        if (!_cached)
        {
            return;
        }

        ((RectTransform)_bossBoard.transform).anchoredPosition = _boardPosition;
        _noticeBackground.color = _noticeColor;
        _HpBarForwardImage.color = _forwardColor;
        _boardGroup.alpha = 1f;
    }

    #endregion

#if UNITY_EDITOR
    #region 임시 데이터 및 테스트

    /// <summary>
    /// 실제 보스와 무관한 이름·HP로 조우 연출을 시작합니다.
    /// </summary>
    [ContextMenu("보스 테스트/조우")]
    public void TestEncounter()
    {
        if (CanTestAnimation())
        {
            ShowBoss(_testBossName, _testMaxHp, _testMaxHp);
        }
    }

    /// <summary>
    /// 현재 표시용 HP만 감소시킵니다. 실제 보스 데이터에는 접근하지 않습니다.
    /// </summary>
    [ContextMenu("보스 테스트/일부 피해")]
    public void TestDamage()
    {
        if (!CanTestAnimation())
        {
            return;
        }

        gameObject.SetActive(true);
        if (_testDamageRoutine != null)
        {
            StopCoroutine(_testDamageRoutine);
        }

        _testDamageRoutine = StartCoroutine(TestDamageAfterContextMenu());
    }

    /// <summary>
    /// 메뉴가 닫힌 직후의 누적 비배율 프레임 시간을 넘긴 뒤 피해 연출을 시작합니다.
    /// </summary>
    System.Collections.IEnumerator TestDamageAfterContextMenu()
    {
        // DOTween 연출 테스트: ContextMenu 동안 멈춘 Editor의 큰 unscaledDeltaTime이 지연·감소를 한 번에 끝내지 않게 합니다.
        // 첫 재개 프레임의 DOTween 갱신과 코루틴 재개를 넘기고, 다음 정상 프레임에서 새 Tween을 생성합니다.
        yield return null;
        yield return null;
        _testDamageRoutine = null;
        if (!_hasBoss || _dying)
        {
            ShowBoss(_testBossName, _testMaxHp, _testMaxHp);
        }

        if (_noticing)
        {
            _noticeTween?.Complete();
        }

        ApplyDamage(_testDamage);
    }

    /// <summary>
    /// 테스트 보스의 HP를 초기화하고 짧은 간격으로 실제 피해 표시 경로를 반복 실행합니다.
    /// </summary>
    [ContextMenu("보스 테스트/연속 피해")]
    public void TestRepeatedDamage()
    {
        if (!CanTestAnimation())
        {
            return;
        }

        // 기존 테스트를 정리하고 표시용 데이터만 초기화합니다. 실제 보스 데이터에는 접근하지 않습니다.
        ShowBoss(_testBossName, _testMaxHp, _testMaxHp);
        if (!_hasBoss)
        {
            return;
        }

        _testDamageRoutine = StartCoroutine(TestRepeatedDamageAfterContextMenu());
    }

    /// <summary>
    /// 메뉴 종료 직후 누적 프레임 시간을 넘긴 뒤 비배율 시간 간격으로 연속 피해를 적용합니다.
    /// </summary>
    System.Collections.IEnumerator TestRepeatedDamageAfterContextMenu()
    {
        // DOTween 연출 테스트: ContextMenu 종료 직후 큰 unscaledDeltaTime이 새 피해 연출을 건너뛰지 않도록 두 프레임 대기합니다.
        yield return null;
        yield return null;
        if (_noticing)
        {
            _noticeTween?.Complete();
        }

        int count = Mathf.Max(1, _testRepeatedDamageCount); // 실행할 전체 타격 횟수입니다.
        float interval = Mathf.Max(.01f, _testRepeatedDamageInterval); // 시간 배율과 무관한 타격 간격입니다.
        for (int i = 0; i < count; i++)
        {
            if (!_hasBoss || _dying)
            {
                break;
            }

            ApplyDamage(_testRepeatedDamage);
            if (i < count - 1 && !_dying)
            {
                yield return new WaitForSecondsRealtime(interval);
            }
        }

        _testDamageRoutine = null;
    }

    /// <summary>
    /// 표시용 HP를 0으로 만들어 사망 연출을 확인합니다.
    /// </summary>
    [ContextMenu("보스 테스트/사망")]
    public void TestDeath()
    {
        if (CanTestAnimation() && _hasBoss)
        {
            UpdateHp(0f);
        }
    }

    /// <summary>
    /// 연출과 표시 상태를 즉시 초기화합니다.
    /// </summary>
    [ContextMenu("보스 테스트/초기화")]
    public void TestReset()
    {
        HideBoss();
    }

    /// <summary>
    /// 연출 테스트는 프리팹 에셋이 아닌 플레이 중 씬 인스턴스에서만 실행합니다.
    /// </summary>
    bool CanTestAnimation()
    {
        if (Application.isPlaying && gameObject.scene.IsValid() && gameObject.scene.isLoaded)
        {
            return true;
        }

        Debug.LogWarning("BossView: 씬 인스턴스에서 플레이 모드로 연출을 테스트해 주세요.", this);
        return false;
    }
    #endregion
#endif

}

