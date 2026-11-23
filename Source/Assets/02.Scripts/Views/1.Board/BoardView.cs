using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선상 NPC와의 상호작용 종류입니다.
/// </summary>
public enum NPCInteractType
{
    Talk,
    Store,
    Upgrade,
    None
}

/// <summary>
/// 선상 뷰
/// </summary>
[DefaultExecutionOrder(-9000)]
public class BoardView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [Header("잠수")]
    [SerializeField] Button _diveButton; // 잠수 버튼

    [Header("NPC")]
    [SerializeField] Button _interactNPCButton; // 상호작용 버튼
    [SerializeField] NPCInteractType _currentInteractType = NPCInteractType.None; // 현재 상호작용 종류 값입니다.

    #endregion

    #region 런타임 상태

    [Header("상호작용 선택지별 뷰")]
    TalkView _talkView; // 대화 뷰 표시를 연결할 뷰입니다.

    #endregion

    #region UI 연결 및 표시 설정

    [SerializeField] StoreView _storeView; // 상점 뷰 표시를 연결할 뷰입니다.
    [SerializeField] UpgradeEquipView _upgradeEquipView; // 강화 장비 뷰 표시를 연결할 뷰입니다.

    [Header("다중 씬 공통 뷰")]
    [SerializeField] BaseView _baseView; // 기본 뷰 표시를 연결할 뷰입니다.

    [Header("NPC 선택 강조 표시")]
    [SerializeField] RectTransform _npcSelectionBackground; // Interact_NPC/SelectionBackground를 연결합니다.
    [SerializeField] RectTransform _npcSelectionArrow; // Interact_NPC/SelectionArrow를 연결합니다.
    [SerializeField] TMPro.TMP_Text[] _npcOptionTexts; // 대화·상점·장비강화 텍스트를 순서대로 연결합니다.
    bool _npcChoiceDismissed; // ESC로 닫은 선택창을 같은 근접 상태 갱신이 다시 열지 않도록 합니다.
    [SerializeField] BoatNPCInteractionController _npcInteractionController; // 같은 씬의 선택 담당자입니다. 하나만 있으면 자동 연결합니다.

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 잠수와 NPC 상호작용 버튼을 연결하고 공통 대화 뷰를 참조합니다.
    /// </summary>
    private void Awake()
    {
        _diveButton.onClick.AddListener(() =>
        {
        });
        _interactNPCButton.onClick.AddListener(() =>
        {
            switch (_currentInteractType)
            {
                case NPCInteractType.Talk:
                    break;
                case NPCInteractType.Store:
                    break;
                case NPCInteractType.Upgrade:
                    break;
                case NPCInteractType.None:
                    break;
            }
        });
        _talkView = _baseView != null ? _baseView.TalkView : null;
        InitializeNPCSelection();
        PlaceNPCSelection();
    }

    /// <summary>
    /// 잠수와 NPC 상호작용 버튼을 초기 상태에서 숨깁니다.
    /// </summary>
    private void Start()
    {
        SetDiveIcon(false);
        SetInteractSelect(false);
    }

    /// <summary>
    /// 기존 상호작용 담당자의 선택값을 읽어 UI만 갱신합니다.
    /// </summary>
    void Update() => ConfirmNPCSelectionFromKeyboard();

    void LateUpdate()
    {
        PlaceDiveInteraction();
        if (_interactNPCButton != null && _interactNPCButton.gameObject.activeInHierarchy)
        {
            RefreshNPCSelection();
            PlaceNPCSelection();
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 같은 씬의 Canvas_Base에 있는 BaseView와 공통 대화 화면을 연결합니다.
    /// </summary>
    /// <param name="baseView">기본 뷰 표시를 연결할 뷰입니다.</param>
    public void BindCommonView(BaseView baseView)
    {
        // BoardCanvasView가 호출합니다. Inspector에서는 Canvas_Base/BasePanel를 연결합니다.
        _baseView = baseView;
        _talkView = _baseView != null ? _baseView.TalkView : null;
    }

    Transform _diveHero;
    public void BindDiveHero(Transform hero) => _diveHero = hero;
    void PlaceDiveInteraction()
    {
        if (_diveHero == null || _diveButton == null || !_diveButton.gameObject.activeInHierarchy || Camera.main == null) return;
        var rect = _diveButton.transform as RectTransform;
        var parent = rect.parent as RectTransform;
        var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        var screen = Camera.main.WorldToScreenPoint(_diveHero.position);
        if (screen.z <= 0f) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var point)) return;
        rect.pivot = new Vector2(.5f, 1f);
        point.y -= 16f;
        point.y = Mathf.Max(point.y, parent.rect.yMin + rect.rect.height + 12f);
        rect.localPosition = new Vector3(rect.localPosition.x, point.y, 0f);
    }

    Transform _npcAnchor;
    Renderer[] _npcRenderers;

    public void BindNPCAnchor(Transform target)
    {
        _npcAnchor = target;
        _npcRenderers = target != null ? target.GetComponentsInChildren<Renderer>() : null;
        PlaceNPCSelection();
    }

    /// <summary>NPC 머리 우상단을 캔버스 좌표로 변환해 선택창을 붙입니다.</summary>
    void PlaceNPCSelection()
    {
        if (_npcAnchor == null || _interactNPCButton == null ||
            !(_interactNPCButton.transform is RectTransform rect) ||
            !(rect.parent is RectTransform parent)) return;
        var camera = Camera.main;
        if (camera == null) return;
        Vector3 head = _npcAnchor.position + Vector3.up * 1.8f;
        bool hasBounds = false;
        Bounds bounds = default;
        if (_npcRenderers != null)
            foreach (var renderer in _npcRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer is ParticleSystemRenderer) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
        if (hasBounds) head = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        var screen = camera.WorldToScreenPoint(head);
        if (screen.z <= 0f) return;
        var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out var local)) return;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(0f, .35f);
        rect.localRotation = Quaternion.identity;
        local += new Vector2(40f, -16f);
        local.x = Mathf.Clamp(local.x, parent.rect.xMin + 16f, Mathf.Max(parent.rect.xMin + 16f, parent.rect.xMax - rect.rect.width - 16f));
        local.y = Mathf.Clamp(local.y, parent.rect.yMin + rect.rect.height * rect.pivot.y + 16f, Mathf.Max(parent.rect.yMin + rect.rect.height * rect.pivot.y + 16f, parent.rect.yMax - rect.rect.height * (1f - rect.pivot.y) - 16f));
        rect.localPosition = new Vector3(local.x, local.y, 0f);
    }

    /// <summary>
    /// 같은 씬에 선택 담당자가 하나만 있을 때 연결하고 표시를 초기화합니다.
    /// </summary>
    void InitializeNPCSelection()
    {
        if (_npcInteractionController == null)
        {
            BoatNPCInteractionController candidate = null; // 후보 값입니다.
            int count = 0; // 개수 값입니다.
            // controller: controller 값입니다.
            foreach (var controller in FindObjectsByType<BoatNPCInteractionController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
                if (controller.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                candidate = controller;
                count++;
            }

            if (count == 1)
            {
                _npcInteractionController = candidate;
            }
        }

        RefreshNPCSelection();
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 잠수 버튼을 표시하거나 숨깁니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    public void SetDiveIcon(bool isOn)
    {
        _diveButton.gameObject.SetActive(isOn);
    }

    /// <summary>
    /// NPC 상호작용 선택 버튼을 표시하거나 숨깁니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    public void SetInteractSelect(bool isOn)
    {
        if (!isOn) _npcChoiceDismissed = false;
        if (isOn && _npcChoiceDismissed) return;
        var effect = GetComponent<UIVisibilityEffectView>();
        if (effect != null) { if (isOn) effect.Show(); else effect.Hide(); }
        else _interactNPCButton.gameObject.SetActive(isOn);
        RefreshNPCSelection();
    }

    /// <summary>앞 창이 없는 경우 ESC가 스마트폰보다 먼저 선택창을 닫도록 공통 취소 View에서 호출합니다.</summary>
    internal bool TryCancelNPCInteraction()
    {
        if (!NPCChoiceVisible()) return false;
        SetInteractSelect(false);
        _npcChoiceDismissed = true;
        var events = UnityEngine.EventSystems.EventSystem.current;
        if (events != null && events.currentSelectedGameObject != null &&
            events.currentSelectedGameObject.transform.IsChildOf(_interactNPCButton.transform))
            events.SetSelectedGameObject(null);
        return true;
    }

    /// <summary>다른 앱 뒤에 가려진 선택창과 열려 있는 NPC 상세창에는 확인 입력을 전달하지 않습니다.</summary>
    bool NPCChoiceVisible()
    {
        if (!isActiveAndEnabled || _interactNPCButton == null || !_interactNPCButton.gameObject.activeInHierarchy) return false;
        if (_npcInteractionController != null && _npcInteractionController.IsViewOpen) return false;
        foreach (var group in _interactNPCButton.GetComponentsInParent<CanvasGroup>())
            if (group.alpha <= .001f) return false;
        return true;
    }

    /// <summary>현재 강조된 NPC 항목은 기존 버튼 이벤트로 실행하며 EventSystem의 Submit과 중복 호출하지 않습니다.</summary>
    void ConfirmNPCSelectionFromKeyboard()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null || (!keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame) ||
            _npcChoiceDismissed || !NPCChoiceVisible() || !_interactNPCButton.IsInteractable()) return;
        if (_npcInteractionController != null && !_npcInteractionController.IsNearby) return;
        var effect = GetComponent<UIVisibilityEffectView>();
        if (effect != null && effect.IsHiding) return;
        var events = UnityEngine.EventSystems.EventSystem.current;
        // EventSystem보다 먼저 NPC 버튼을 선택해 Enter가 스마트폰 등 뒤쪽 버튼에 전달되지 않게 합니다.
        if (events != null && events.currentInputModule is UnityEngine.InputSystem.UI.InputSystemUIInputModule module &&
            module.submit != null && module.submit.action != null && module.submit.action.enabled &&
            module.submit.action.WasPerformedThisFrame())
        {
            events.SetSelectedGameObject(_interactNPCButton.gameObject);
            return; // 기존 Submit이 같은 버튼을 한 번만 실행합니다.
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        if (events != null && events.currentInputModule is UnityEngine.EventSystems.StandaloneInputModule legacy &&
            UnityEngine.Input.GetButtonDown(legacy.submitButton))
        {
            events.SetSelectedGameObject(_interactNPCButton.gameObject);
            return;
        }
#endif
        _interactNPCButton.onClick.Invoke();
    }

    /// <summary>
    /// 외부 표시 연결이나 미리보기에서 사용할 선택 항목을 지정합니다. 실제 창은 열지 않습니다.
    /// </summary>
    /// <param name="type">종류 값입니다.</param>
    public void SetNPCSelection(NPCInteractType type)
    {
        _currentInteractType = type;
        RefreshNPCSelection();
    }

    /// <summary>
    /// 선택 행에 배경과 화살표를 맞추고 글씨는 기존 밝은 색으로 유지합니다.
    /// </summary>
    void RefreshNPCSelection()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_npcOptionTexts == null || _npcOptionTexts.Length == 0)
        {
            return;
        }

        var selected = _npcInteractionController != null ? _npcInteractionController.SelectedInteraction : _currentInteractType; // 선택 값입니다.
        int index = selected == NPCInteractType.None ? 0 : (int)selected; // 인덱스 값입니다.
        bool valid = index >= 0 && index < _npcOptionTexts.Length && _npcOptionTexts[index] != null; // 유효 여부입니다.
        if (_npcSelectionBackground != null)
        {
            _npcSelectionBackground.gameObject.SetActive(valid);
        }

        if (_npcSelectionArrow != null)
        {
            _npcSelectionArrow.gameObject.SetActive(valid);
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!valid)
        {
            return;
        }

        float y = _npcOptionTexts[index].rectTransform.anchoredPosition.y; // 세로 값입니다.
        if (_npcSelectionBackground != null)
        {
            _npcSelectionBackground.anchoredPosition = new Vector2(16f, y);
        }

        if (_npcSelectionArrow != null)
        {
            _npcSelectionArrow.anchoredPosition = new Vector2(-64f, y);
        }

        // label: 문구 값입니다.
        foreach (var label in _npcOptionTexts)
        {
            if (label != null)
            {
                label.color = new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f);
            }
        }
    }

    #endregion

}
