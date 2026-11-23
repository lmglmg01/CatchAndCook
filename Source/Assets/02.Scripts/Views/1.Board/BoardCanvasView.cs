using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 독립된 선상 Canvas와 같은 씬의 공통 Canvas 사이의 표시·참조 연결을 관리합니다.
/// </summary>
[DefaultExecutionOrder(-1200)]
public class BoardCanvasView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [Header("공통 Canvas 연결")]
    [SerializeField] BaseView _commonView; // 같은 씬 Canvas_Base/BasePanel의 BaseView. 비워 두면 같은 씬에서 하나만 찾습니다.
    [SerializeField] BoardView _boardView; // 이 Canvas_Board 루트의 BoardView를 연결합니다.
    [SerializeField] StoreView _storeView; // Interact_NPC_StoreWindow의 StoreView를 연결합니다.

    [Header("선상 전용 표시 영역")]
    [SerializeField] CanvasGroup[] _interactionGroups; // 잠수 버튼과 NPC 선택창의 CanvasGroup을 연결합니다.
    [SerializeField] GameObject _storeWindow; // 상점의 실제 표시 영역인 Store_Main을 연결합니다.
    [SerializeField] GameObject _upgradeWindow; // 장비 강화창 루트를 연결합니다.
    [SerializeField] GameObject _resultWindow; // 잠수 결과창 루트를 연결합니다.

    #endregion

    #region 런타임 상태

    // _commonHud: 공통 HUD 값입니다.
    // _commonGold: 공통 골드 값입니다.
    CanvasGroup _commonHud, _commonGold;
    RectTransform _phone; // 스마트폰 값입니다.
    GameObject _appPanel; // 앱 패널 값입니다.
    readonly Dictionary<CanvasGroup, GroupState> _hiddenGroups = new Dictionary<CanvasGroup, GroupState>(); // 숨김 그룹 목록에 사용하는 대상별 매핑입니다.
    readonly HashSet<CanvasGroup> _requestedGroups = new HashSet<CanvasGroup>(); // 요청 그룹 목록 값입니다.

    #endregion

    #region 표시 데이터 형식

    /// <summary>
    /// 일시적으로 숨긴 UI의 기존 표시 및 입력 상태입니다.
    /// </summary>
    struct GroupState
    {

        #region 멤버

        #region 런타임 상태

        public float Alpha; // 투명도 값입니다.
        // Interactable: 상호작용 가능 여부입니다.
        // BlocksRaycasts: 차단 포인터 감지 여부입니다.
        public bool Interactable, BlocksRaycasts;

        #endregion

        #endregion

    }

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 다른 선상 View가 초기화되기 전에 공통 UI를 연결합니다.
    /// </summary>
    void Awake()
    {
        BindCommonCanvas();
    }

    /// <summary>
    /// 공통 앱과 선상 창의 표시 상태에 따라 뒤쪽 UI와 입력을 가립니다.
    /// </summary>
    void LateUpdate()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_commonView == null)
        {
            return;
        }

        _requestedGroups.Clear();
        bool appOpen = _commonView.CurrentAppIndex >= 0; // 앱 열기 여부입니다.
        // 앱이 막 켜진 프레임에도 BaseView.LateUpdate의 호출 순서와 무관하게 판정합니다.
        if (_appPanel != null && _appPanel.activeInHierarchy)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _appPanel.transform.childCount; i++)
            {
                appOpen |= _appPanel.transform.GetChild(i).gameObject.activeInHierarchy;
            }
        }

        bool storeOpen = IsVisible(_storeWindow); // 상점 열기 여부입니다.
        bool upgradeOpen = IsVisible(_upgradeWindow); // 강화 열기 여부입니다.
        bool resultOpen = IsVisible(_resultWindow); // 결과 열기 여부입니다.
        // 공통 앱과 선상 창이 겹쳐 열려도 이 View 한 곳에서 원래 상태를 보관·복원합니다.
        bool blockInteraction = appOpen || IsPhoneVisible() || storeOpen || upgradeOpen || resultOpen || _commonView.IsTalkMode;
        if (blockInteraction && _interactionGroups != null)
        {
            // group: 그룹 값입니다.
            foreach (var group in _interactionGroups)
            {
                RequestHide(group);
            }
        }

        if (storeOpen || upgradeOpen || resultOpen)
        {
            RequestHide(_commonHud);
        }

        if (upgradeOpen || resultOpen)
        {
            RequestHide(_commonGold);
        }

        // 대화 중에는 BaseView가 전체 Canvas의 상태를 보관하므로 변경·복원하지 않습니다.
        if (_commonView.IsTalkMode)
        {
            return;
        }

        var restored = new List<CanvasGroup>(); // 복원 값입니다.
        // pair: pair 값입니다.
        foreach (var pair in _hiddenGroups)
        {
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (_requestedGroups.Contains(pair.Key))
            {
                continue;
            }

            Restore(pair.Key, pair.Value);
            restored.Add(pair.Key);
        }

        // group: 그룹 값입니다.
        foreach (var group in restored)
        {
            _hiddenGroups.Remove(group);
        }

        // group: 그룹 값입니다.
        foreach (var group in _requestedGroups)
        {
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (group == null)
            {
                continue;
            }

            if (!_hiddenGroups.ContainsKey(group))
            {
                _hiddenGroups.Add(group, new GroupState { Alpha = group.alpha, Interactable = group.interactable, BlocksRaycasts = group.blocksRaycasts });
            }

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// 선상 Canvas가 꺼질 때 이 View가 숨긴 공통 UI를 복원합니다.
    /// </summary>
    void OnDisable()
    {
        // pair: pair 값입니다.
        foreach (var pair in _hiddenGroups)
        {
            Restore(pair.Key, pair.Value);
        }

        _hiddenGroups.Clear();
        _requestedGroups.Clear();
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 직접 연결한 공통 View를 우선하며 자동 연결은 같은 씬에 하나만 있을 때 사용합니다.
    /// </summary>
    public void BindCommonCanvas()
    {
        if (_commonView == null)
        {
            BaseView found = null; // 검색 결과 표시를 연결할 뷰입니다.
            // view: UI 동작과 데이터를 연결하는 뷰입니다.
            foreach (var view in FindObjectsByType<BaseView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
                if (view.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
                if (found != null)
                {
                    return; // 여러 공통 Canvas가 있으면 Inspector에서 대상을 지정합니다.
                }

                found = view;
            }

            _commonView = found;
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_commonView == null)
        {
            return;
        }

        if (_boardView != null)
        {
            _boardView.BindCommonView(_commonView);
        }

        var canvas = _commonView.GetComponentInParent<Canvas>(); // 캔버스 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (canvas == null)
        {
            return;
        }

        _commonHud = _commonView.GetComponent<CanvasGroup>();
        var gold = canvas.transform.Find("OwnGoldWindow"); // 골드 값입니다.
        _commonGold = gold != null ? gold.GetComponent<CanvasGroup>() : null;
        if (_storeView != null && gold != null)
        {
            _storeView.BindGoldText(gold.GetComponentInChildren<TMPro.TMP_Text>(true));
        }

        _phone = _commonView.transform.Find("SmartPhone/AppGroup") as RectTransform;
        var panel = canvas.transform.Find("SmartPhonePanel"); // 패널 값입니다.
        _appPanel = panel != null ? panel.gameObject : null;
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    /// <summary>
    /// 폰이 화면 경계 안으로 들어왔는지 확인해 선상 상호작용을 가립니다.
    /// </summary>
    bool IsPhoneVisible()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_phone == null || !_phone.gameObject.activeInHierarchy)
        {
            return false;
        }

        var canvas = _phone.GetComponentInParent<Canvas>(); // 캔버스 값입니다.
        var rect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null; // 사각 영역 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (rect == null)
        {
            return false;
        }

        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, _phone); // 경계 값입니다.
        return bounds.max.y > rect.rect.yMin && bounds.min.y < rect.rect.yMax && bounds.max.x > rect.rect.xMin && bounds.min.x < rect.rect.xMax;
    }

    /// <summary>
    /// 지정된 창이 실제 계층에서 활성화되었는지 확인합니다.
    /// </summary>
    /// <param name="window">창 값입니다.</param>
    bool IsVisible(GameObject window)
    {
        return window != null && window.activeInHierarchy;
    }

    #endregion

    #region 선택 및 외부 요청

    /// <summary>
    /// 이번 프레임에 숨겨야 할 영역을 중복 없이 등록합니다.
    /// </summary>
    /// <param name="group">그룹 값입니다.</param>
    void RequestHide(CanvasGroup group)
    {
        if (group != null)
        {
            _requestedGroups.Add(group);
        }
    }

    #endregion

    #region 표시 연출 및 정리

    /// <summary>
    /// 숨기기 전에 보관한 표시·입력 상태를 복원합니다.
    /// </summary>
    /// <param name="group">그룹 값입니다.</param>
    /// <param name="state">상태 값입니다.</param>
    void Restore(CanvasGroup group, GroupState state)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (group == null)
        {
            return;
        }

        group.alpha = state.Alpha;
        group.interactable = state.Interactable;
        group.blocksRaycasts = state.BlocksRaycasts;
    }

    #endregion

}
