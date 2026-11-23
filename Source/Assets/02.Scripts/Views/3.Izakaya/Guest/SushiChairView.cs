using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>의자에 주문·잔반 UI를 고정하고 앞줄 기준 화면 크기를 유지합니다. 주문·청소 판정은 기존 로직에 맡깁니다.</summary>
public sealed class SushiChairView : MonoBehaviour
{
    [SerializeField] GameObject _displayRoot;
    [SerializeField] UnityEngine.UI.Image _foodImage;
    [SerializeField] Sprite _leftoversIcon; // 잔반 표시는 주문한 요리 대신 이 아이콘을 우선 사용합니다.
    [SerializeField] GameObject _keyRoot;
    [SerializeField] TMP_Text _keyText;
    [SerializeField] TMP_Text _noticeText;
    [SerializeField] GameObject _gaugeRoot;
    [SerializeField] UnityEngine.UI.Image _gaugeImage;
    [SerializeField] InputActionReference _interactAction;
    [SerializeField] Vector3 _worldOffset = new Vector3(0f, .15f, 0f);
    public Vector3 WorldOffset => _worldOffset;
    public bool IsVisible => _displayRoot != null && _displayRoot.activeSelf;
    public float Progress { get; private set; }
    public bool CanClean { get; private set; }

    [Header("의자 주문 UI와 원근 보정")]
    [SerializeField] NPCOrderView _orderView;
    [SerializeField] Vector3 _orderWorldOffset = new Vector3(0, 1.42f, 0);
    [SerializeField, Min(.00001f)] float _referenceWorldScale = .01f;
    [SerializeField, Min(.00001f)] float _orderReferenceWorldScale = .0135f;
    Chair _seat;
    GuestNPC _guest;
    [SerializeField] Transform _seatRoot; // 씬에 미리 배치한 중립 부모를 연결하며 실행 중 계층을 유지합니다.
    Transform _referenceSeat;
    public Chair Seat => _seat;
    public GuestNPC Guest => _guest;
    public NPCOrderView OrderView => _orderView;

    SushiCleanupBubbleView _bubblePresentation;
    Canvas _orderCanvas;
    SushiCleanupBubbleView BubblePresentation => _bubblePresentation != null ? _bubblePresentation : (_bubblePresentation = GetComponent<SushiCleanupBubbleView>());
    Transform _anchor;
    /// <summary>주문과 잔반 Canvas를 실제 의자 좌석 아래 고정합니다. 모델의 공용 서빙 영역은 사용하지 않습니다.</summary>
    public void BindSeat(Chair seat, Transform reference)
    {
        if (seat == null) return;
        _anchor = null; _seat = seat; _referenceSeat = reference;
        var point = seat.SeatPos != null ? seat.SeatPos : seat.transform;
        // 씬에 저장된 부모는 재생성·재배치하지 않고 위치와 원근 배율만 갱신합니다.
        if (_seatRoot != null && _seatRoot.IsChildOf(point) && transform.IsChildOf(_seatRoot))
        {
            ApplySeatPose();
            return;
        }
        _seatRoot = point.Find("ChairUI");
        if (_seatRoot == null)
        {
            _seatRoot = new GameObject("ChairUI").transform;
            _seatRoot.SetParent(point, false);
        }
        transform.SetParent(_seatRoot, false);
        ApplySeatPose();
    }

    /// <summary>같은 의자의 주문 표시를 새 손님에게 연결합니다. 같은 손님의 매 프레임 재초기화는 하지 않습니다.</summary>
    public void BindGuest(GuestNPC guest)
    {
        if (_guest == guest) return;
        _guest = guest;
        if (_orderView != null) _orderView.BindGuest(guest);
        ApplySeatPose();
    }

    /// <summary>이전 손님의 늦은 퇴장 알림이 새 손님의 주문을 숨기지 않도록 소유자를 확인합니다.</summary>
    public void ReleaseGuest(GuestNPC guest)
    {
        if (_guest != guest) return;
        BindGuest(null);
    }

    /// <summary>의자의 비균일 배율을 제거하고 각 Canvas의 실제 깊이에 따라 화면 크기만 보정합니다.</summary>
    public void ApplySeatPose()
    {
        if (_seat == null || _seatRoot == null) return;
        var point = _seat.SeatPos != null ? _seat.SeatPos : _seat.transform;
        _seatRoot.position = point.position; _seatRoot.rotation = Quaternion.identity;
        var inherited = _seatRoot.parent.lossyScale;
        _seatRoot.localScale = new Vector3(Inverse(inherited.x), Inverse(inherited.y), Inverse(inherited.z));
        var camera = Camera.main;
        if (camera == null) return;
        PlaceCanvas(transform, _worldOffset, _referenceWorldScale, camera, point);
        if (_orderView != null)
        {
            PlaceCanvas(_orderView.transform, _orderWorldOffset, _orderReferenceWorldScale, camera, point);
            if (_orderCanvas == null) _orderCanvas = _orderView.GetComponent<Canvas>();
            if (_orderCanvas != null)
            {
                // 뒷줄 주문(0) < 플레이어 서빙(1) < 앞줄 주문(2) 순서를 유지합니다.
                _orderCanvas.overrideSorting = true;
                _orderCanvas.sortingOrder = _seat.Area == ChairArea.Front ? 2 : 0;
            }
        }
    }

    /// <summary>카메라와의 거리를 강제로 같게 만들지 않고, 앞줄 기준 투영 크기를 유지합니다.</summary>
    void PlaceCanvas(Transform canvas, Vector3 offset, float baseScale, Camera camera, Transform point)
    {
        canvas.position = point.position + offset; canvas.rotation = camera.transform.rotation;
        float ratio = 1f;
        if (!camera.orthographic && _referenceSeat != null)
        {
            float depth = Vector3.Dot(camera.transform.forward, canvas.position - camera.transform.position);
            float reference = Vector3.Dot(camera.transform.forward, _referenceSeat.position + offset - camera.transform.position);
            if (depth > .001f && reference > .001f) ratio = depth / reference;
        }
        var parentScale = canvas.parent != null ? canvas.parent.lossyScale : Vector3.one;
        float scale = baseScale * ratio;
        canvas.localScale = new Vector3(scale * Inverse(parentScale.x), scale * Inverse(parentScale.y), scale * Inverse(parentScale.z));
    }

    static float Inverse(float value) => Mathf.Abs(value) > .00001f ? 1f / value : 1f;

    /// <summary>의자 배율을 상속하지 않고 좌석 위치만 따라갈 기준을 연결합니다.</summary>
    public void BindAnchor(Transform anchor) { _anchor = anchor; }

    /// <summary>표시 데이터 판정과 별도로 위치와 카메라 방향만 갱신합니다.</summary>
    void LateUpdate()
    {
        if (_seat != null) { ApplySeatPose(); return; }
        if (_anchor == null || !IsVisible) return;
        transform.position = _anchor.position + _worldOffset;
        var camera = Camera.main;
        if (camera != null) transform.rotation = camera.transform.rotation;
    }

    void Awake() => Hide();
    void OnDisable() => Hide();

    /// <summary>모델에서 받은 표시값만 반영하며 청소 입력과 진행 시간은 계산하지 않습니다.</summary>
    public void Display(Sprite icon, bool uncleaned, float progress, bool canClean)
    {
        if (!uncleaned) { Hide(); return; }
        Progress = float.IsNaN(progress) ? 0f : Mathf.Clamp01(progress);
        CanClean = canClean;
        if (_displayRoot != null) _displayRoot.SetActive(true);
        GetComponent<UIVisibilityEffectView>()?.Show();
        if (_foodImage != null)
        {
            var displayIcon = _leftoversIcon != null ? _leftoversIcon : icon;
            _foodImage.sprite = displayIcon;
            _foodImage.color = Color.white;
            _foodImage.enabled = displayIcon != null;
        }
        if (_keyRoot != null) _keyRoot.SetActive(canClean);
        if (_gaugeRoot != null) _gaugeRoot.SetActive(Progress > 0f);
        if (_gaugeImage != null) _gaugeImage.fillAmount = Progress;
        if (_noticeText != null) _noticeText.text = Progress > 0f ? "청소 중" : canClean ? "길게 눌러 청소" : "잔반 정리";
        if (_keyText != null && _interactAction != null && _interactAction.action != null)
        {
            var action = _interactAction.action;
            for (int i = 0; i < action.bindings.Count; i++)
                if (!action.bindings[i].isPartOfComposite && action.bindings[i].effectivePath != null && action.bindings[i].effectivePath.Contains("<Keyboard>"))
                { _keyText.text = action.GetBindingDisplayString(i); break; }
        }
        BubblePresentation?.Present(true,null,true,Progress>0f,canClean,Progress,
            _keyText!=null&&!string.IsNullOrEmpty(_keyText.text)?_keyText.text:"F");
    }

    /// <summary>청소 완료·비활성화 시 진행도와 안내를 함께 비웁니다.</summary>
    public void Hide()
    {
        var effect = GetComponent<UIVisibilityEffectView>();
        if (effect != null && effect.HideAfter(ClearCleaningPresentation)) return;
        ClearCleaningPresentation();
    }

    void ClearCleaningPresentation()
    {
        Progress = 0f; CanClean = false;
        if (_displayRoot != null) _displayRoot.SetActive(false);
        if (_gaugeImage != null) _gaugeImage.fillAmount = 0f;
        if (_gaugeRoot != null) _gaugeRoot.SetActive(false);
        if (_keyRoot != null) _keyRoot.SetActive(false);
    }
}
