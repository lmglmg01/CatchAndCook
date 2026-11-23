using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>강화 실패 안내와 확인 버튼을 표시합니다. 장비와 재화 데이터는 변경하지 않습니다.</summary>
public class UpgradeFailurePopupView : MonoBehaviour
{
    [SerializeField] GameObject _overlay;
    [SerializeField] TMP_Text _message;
    [SerializeField] UnityEngine.UI.Button _confirm;
    GameObject _previousSelection;
    public bool IsOpen => _overlay != null && _overlay.activeSelf;

    /// <summary>확인 버튼의 닫기 동작을 한 번 연결합니다.</summary>
    void Awake() {
        UIEscapeView.Register(this, _confirm); _confirm.onClick.AddListener(Close); }

    /// <summary>실패 원인에 맞는 안내를 표시하고 키보드 선택도 확인 버튼으로 이동합니다.</summary>
    public void Show(bool insufficientGold)
    {
        if (!IsOpen && EventSystem.current != null) _previousSelection = EventSystem.current.currentSelectedGameObject;
        _message.text = insufficientGold ? "재화가 부족하여 강화할 수 없습니다." : "강화할 수 없습니다.\n장비 상태를 확인해주세요.";
        _overlay.SetActive(true);
        _overlay.transform.SetAsLastSibling();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_confirm.gameObject);
    }

    /// <summary>팝업을 닫고 이전 UI 선택을 복원합니다. 장비 선택은 그대로 유지합니다.</summary>
    public void Close()
    {
        if (!IsOpen) return;
        _overlay.SetActive(false);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
        _previousSelection = null;
    }

    /// <summary>상점 화면 전환 등으로 강화 창이 닫힐 때 팝업 상태도 초기화합니다.</summary>
    void OnDisable() { Close(); }

    /// <summary>이 컴포넌트가 등록한 버튼 이벤트만 해제합니다.</summary>
    void OnDestroy() { if (_confirm != null) _confirm.onClick.RemoveListener(Close); }
}
