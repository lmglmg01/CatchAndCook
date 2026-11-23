using TMPro;
using UnityEngine;

/// <summary>
/// 퀘스트 세부 목표 하나의 대상, 진행 수량과 완료 상태를 표시합니다.
/// </summary>
public class QuestConditionView : MonoBehaviour
{
    // 하늘색 원본 게이지를 구상도의 녹색(#31BE6D)으로 표시하는 보정색입니다.
    static readonly Color ProgressGaugeTint = Color.white;
    bool _objectiveComplete;


    #region 멤버

    #region UI 연결 및 표시 설정

    // Condition 바로 아래의 ConditionText TMP_Text를 연결합니다. 목표 이름과 현재 수량/목표 수량을 표시합니다.
    [SerializeField] TMP_Text _conditionText;
    // 해당 목표 행의 TargetImage Image를 연결합니다. 대상 아이콘이 없는 데이터에서는 숨깁니다.
    [SerializeField] UnityEngine.UI.Image _targetImage;
    // StateBox/StateText TMP_Text를 연결합니다. 목표의 진행 중/완료 문구를 표시하며 ConditionText와 구분합니다.
    [SerializeField] TMP_Text _stateText;

    [Header("에셋 교체용 목표 표시")]
    [SerializeField] TMP_Text _amountText; // 수량 텍스트입니다.
    // Condition의 ProgressTrack/ProgressFill Image를 연결합니다. 배경 마스크 안에서 Horizontal Filled 방식으로 진행률을 표시합니다.
    [SerializeField] UnityEngine.UI.Image _progressFill;
    // Condition 오른쪽 CompleteCheck Image를 연결합니다. 목표 달성 시 체크 아이콘만 표시합니다.
    [SerializeField] UnityEngine.UI.Image _completeIcon;
    // Quest_Check 스프라이트를 연결합니다. 목표 완료 아이콘으로 사용합니다.
    [SerializeField] Sprite _completeSprite;
    [SerializeField] Sprite _normalGaugeSprite, _completedGaugeSprite; // 진행/달성 게이지 모양입니다.
    // 최신 목표 행의 진행 중/완료 배경입니다. 미연결 시 기존 배경을 유지합니다.
    [SerializeField] UnityEngine.UI.Image _conditionBackground;
    [SerializeField] Sprite _inProgressBackgroundSprite, _completedBackgroundSprite;
    [SerializeField, Range(0f, 1f)] float _conditionBackgroundOpacity = .90f;

    #endregion

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 세부 목표의 이름, 수량과 완료 상태를 표시합니다.
    /// </summary>
    /// <param name="objective">목표 값입니다.</param>
    public void Bind(QuestListView.Objective objective)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (objective == null)
        {
            return;
        }

        SetCondition(objective.description, objective.current, objective.target);
        SetState(objective.IsComplete ? "완료" : "진행 중");
        if (_targetImage != null)
        {
            _targetImage.sprite = objective.targetIcon;
            _targetImage.enabled = objective.targetIcon != null;
        }
    }

    #endregion

    /// <summary>클리어한 퀘스트에서는 수량을 숨기고 목표 문구를 회색으로 표시합니다.</summary>
    public void SetCompletedQuest(bool completed)
    {
        if (_amountText != null) _amountText.gameObject.SetActive(!_objectiveComplete);
        if (_progressFill != null)
        {
            _progressFill.gameObject.SetActive(!_objectiveComplete);
            if (_progressFill.transform.parent != null) _progressFill.transform.parent.gameObject.SetActive(!_objectiveComplete);
        }
        if (_conditionText != null) _conditionText.color = completed ? new Color(.56f,.60f,.60f) : new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f);
    }
    #region UI 표시 및 갱신

    /// <summary>
    /// 목표 이름과 유효 범위의 현재 수량을 표시합니다.
    /// </summary>
    /// <param name="content">내용 값입니다.</param>
    /// <param name="currentAmount">현재 수량 값입니다.</param>
    /// <param name="maxAmount">최대 수량 값입니다.</param>
    public void SetCondition(string content, int currentAmount, int maxAmount)
    {
        int target = Mathf.Max(1, maxAmount);
        _objectiveComplete = currentAmount >= target; // 대상 값입니다.
        // 클리어 조건의 슬래시 왼쪽 현재 수량만 밝게 표시하고, 슬래시와 목표 수량은 기존 색을 유지합니다.
        string amount = $"<color=#FDF7E4>{Mathf.Clamp(currentAmount, 0, target)}</color><color=#989486>/{target}</color>";
        if (_conditionText != null)
        {
            if (_amountText == null)
            {
                _conditionText.richText = true;
            }

            _conditionText.text = _amountText != null ? content : $"{content}  {amount}";
        }

        if (_amountText != null)
        {
            _amountText.richText = true;
            _amountText.text = amount;
            _amountText.color = new Color(.30f,.39f,.43f);
        }

        if (_progressFill != null)
        {
            bool achieved = currentAmount >= target;
            var sprite = _normalGaugeSprite;
            if (sprite != null) _progressFill.sprite = sprite;
            _progressFill.color = ProgressGaugeTint;
        }
        SetProgressGauge(currentAmount, target);
        // OP85는 배경 이미지에만 적용하고 글자와 체크 표시의 불투명도는 유지합니다.
        if (_conditionBackground != null)
        {
            var background = currentAmount >= target ? _completedBackgroundSprite : _inProgressBackgroundSprite;
            if (background != null)
            {
                _conditionBackground.sprite = background;
                _conditionBackground.color = new Color(1f, 1f, 1f, _conditionBackgroundOpacity);
            }
        }
        if (_progressFill != null)
        {
            // 달성 상태에서도 게이지와 체크를 함께 표시합니다. 최종 완료 화면의 게이지는 별도로 숨깁니다.
            _progressFill.gameObject.SetActive(!_objectiveComplete);
            var track = _progressFill.transform.parent;
            if (track != null && track.name == "ProgressTrack")
            {
                track.gameObject.SetActive(!_objectiveComplete);
            }
        }
        if (_amountText != null) _amountText.gameObject.SetActive(!_objectiveComplete);
        if (_completeIcon != null)
        {
            _completeIcon.color = Color.white;
            _completeIcon.gameObject.SetActive(currentAmount >= target);
            if (_completeSprite != null)
            {
                _completeIcon.sprite = _completeSprite;
            }

            var fallback = _completeIcon.transform.Find("Placeholder"); // 대체 값입니다.
            if (fallback != null)
            {
                fallback.gameObject.SetActive(_completeSprite == null);
            }
        }
    }

    /// <summary>
    /// 진행도를 fillAmount에 반영합니다. 기존 Sliced UI의 너비 갱신도 호환 유지합니다.
    /// </summary>
    /// <param name="currentAmount">현재 수량 값입니다.</param>
    /// <param name="target">대상 값입니다.</param>
    void SetProgressGauge(int currentAmount, int target)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_progressFill == null)
        {
            return;
        }

        float progress = Mathf.Clamp01((float)currentAmount / target); // 진행도 값입니다.
        _progressFill.fillAmount = progress;
        // Filled 게이지는 fillAmount만 사용하고, 기존 Sliced 참조만 너비로 표시합니다.
        if (_progressFill.type != UnityEngine.UI.Image.Type.Sliced)
        {
            return;
        }

        var track = _progressFill.rectTransform.parent as RectTransform; // 추적 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (track == null)
        {
            return;
        }

        float width = track.rect.width; // 너비 값입니다.
        float minimum = _progressFill.sprite != null ? _progressFill.sprite.border.x + _progressFill.sprite.border.z : 0; // 최소 값입니다.
        // 레퍼런스처럼 0%에서도 작은 시작 표시를 남깁니다.
        _progressFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Clamp(width * progress, Mathf.Min(minimum, width), width));
    }

    /// <summary>
    /// 세부 목표의 완료 또는 진행 상태를 표시합니다.
    /// </summary>
    /// <param name="state">상태 값입니다.</param>
    public void SetState(string state)
    {
        if (_stateText != null)
        {
            _stateText.text = state;
        }
    }

    #endregion

}
