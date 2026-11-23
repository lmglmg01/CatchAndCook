using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 조작 역할 이름·현재 키·선택 대기 상태를 표시하는 단축키 행 뷰입니다.
/// </summary>
public class KeyBindingSlotView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] TMP_Text _roleText; // 행 왼쪽의 역할 이름 TMP_Text를 연결합니다.
    [SerializeField] TMP_Text _descriptionText; // 역할 아래의 설명 TMP_Text를 연결합니다.
    [Header("키 입력")]
    [SerializeField] TMP_InputField _keyInputField;
    [SerializeField] Button _keyButton; // 오른쪽 키 모양 Button을 연결합니다.
    [SerializeField] TMP_Text _keyText; // 키 버튼 안의 현재 키 이름 TMP_Text를 연결합니다.
    [SerializeField] Image _keyBackground; // 선택 대기를 색으로 구분할 키 버튼 Image를 연결합니다.
    [SerializeField] GameObject _selectionMark; // 키 버튼 둘레의 선택 표시 GameObject를 연결합니다.
    [SerializeField] bool _boardStyle; // 선상 스타일 여부입니다.

    #endregion

    #region 런타임 상태

    Tween _colorTween; // 색상 트윈 값입니다.
    Color _lastColor; // 마지막 색상 색상입니다.
    bool _hasColor; // 색상 여부입니다.

    #endregion

    #region 이벤트 액션

    public event Action<int> OnSelected; // 부모 페이지에 선택한 항목의 인덱스를 전달

    #endregion

    #region 런타임 상태

    bool _initialized; // 중복 버튼 연결을 방지

    #endregion

    #region 프로퍼티

    public int EntryIndex { get; private set; } // 부모의 현재 조작 항목 인덱스

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 키 버튼 클릭을 한 번 연결합니다.
    /// </summary>
    void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// 뷰가 꺼질 때 이벤트와 진행 중인 표시를 정리합니다.
    /// </summary>
    void OnDisable()
    {
        _colorTween?.Kill();
        _hasColor = false;
    }

    /// <summary>
    /// 행 제거 시 클릭 이벤트를 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_keyInputField != null) _keyInputField.onSelect.RemoveListener(OnInputSelected);
        if (_keyButton != null)
        {
            _keyButton.onClick.RemoveListener(Select);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 키 버튼의 클릭 영역을 보장하고, 비활성 상태에서 데이터가 전달되어도 이벤트를 중복 등록하지 않습니다.
    /// </summary>
    void Initialize()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (_keyInputField != null) _keyInputField.onSelect.AddListener(OnInputSelected);
        if (_keyInputField == null && _keyButton != null)
        {
            // 에셋의 표시 설정과 관계없이 키 버튼 배경이 포인터 입력을 받도록 보장합니다.
            if (_keyButton.targetGraphic == null && _keyBackground != null)
            {
                _keyButton.targetGraphic = _keyBackground;
            }

            if (_keyButton.targetGraphic != null)
            {
                _keyButton.targetGraphic.raycastTarget = true;
            }

            _keyButton.onClick.AddListener(Select);
        }

        // 키 이름은 배경 버튼을 가리지 않으며, 버튼 전체 영역에서 같은 클릭을 받습니다.
        if (_keyText != null)
        {
            _keyText.raycastTarget = false;
        }
    }

    /// <summary>
    /// 역할과 키를 표시하고 미정·고정 안내 항목은 입력 대기 대상으로 선택되지 않게 합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="role">역할 값입니다.</param>
    /// <param name="description">설명 값입니다.</param>
    /// <param name="key">키 값입니다.</param>
    /// <param name="editable">편집 가능 여부입니다.</param>
    /// <param name="selected">선택 여부입니다.</param>
    public void Bind(int index, string role, string description, string key, bool editable, bool selected)
    {
        Initialize();
        EntryIndex = index;
        if (_roleText != null)
        {
            _roleText.text = role;
        }

        if (_descriptionText != null)
        {
            _descriptionText.text = description;
        }

        if (_keyText != null)
        {
            _keyText.text = selected ? (_boardStyle ? "..." : "입력 대기") : key;
        }

        if (_keyInputField != null)
        {
            _keyInputField.interactable = editable;
            _keyInputField.SetTextWithoutNotify(selected ? "..." : key);
            if (!selected)
            {
                _keyInputField.DeactivateInputField();
                var events = UnityEngine.EventSystems.EventSystem.current;
                if (events != null && events.currentSelectedGameObject == _keyInputField.gameObject)
                    events.SetSelectedGameObject(null);
            }
        }
        if (_keyButton != null) _keyButton.interactable = editable;

        if (_selectionMark != null)
        {
            _selectionMark.SetActive(selected);
        }

        if (_boardStyle)
        {
            var target = selected ? new Color(.12f, .58f, .82f) : Color.white; // 평상시에는 원본 이미지 색을 유지하고 키 변경 대기 중에만 강조합니다.
            // DOTween 연출: 키 변경 대기 버튼의 배경색 전환. 반복 갱신에서는 재시작하지 않습니다.
            if (_keyBackground != null && (!_hasColor || _lastColor != target))
            {
                _colorTween?.Kill();
                _lastColor = target;
                _hasColor = true;
                if (Application.isPlaying && isActiveAndEnabled)
                {
                    _colorTween = _keyBackground.DOColor(target, .12f).SetUpdate(true);
                }
                else
                {
                    _keyBackground.color = target;
                }
            }

            if (_keyText != null)
            {
                _keyText.color = editable ? new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f) : new Color(.57f, .7f, .73f);
            }
        }
        else if (_keyBackground != null)
        {
            _keyBackground.color = selected ? new Color(.98f, .74f, .3f) : editable ? new Color(.77f, .9f, .87f) : new Color(.84f, .85f, .87f);
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 선택한 키 항목을 부모 페이지에 전달합니다.
    /// </summary>
    void OnInputSelected(string value)
    {
        if (_keyInputField != null && _keyInputField.interactable) OnSelected?.Invoke(EntryIndex);
    }

    void Select()
    {
        if (_keyButton != null && _keyButton.interactable)
        {
            OnSelected?.Invoke(EntryIndex);
        }
    }

    #endregion

}
