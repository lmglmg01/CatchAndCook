using TMPro;
using UnityEngine;

/// <summary>ViewTest 전용 임시 UI입니다. 실제 게임 데이터에는 접근하지 않습니다.</summary>
public sealed class UIDirectorTestView : MonoBehaviour
{
    [SerializeField] GameObject _panel;
    [SerializeField] TMP_Text _status;
    [SerializeField] UnityEngine.UI.Button _showButton, _hideButton, _burstButton, _restoreButton, _closeButton;
    int _showCount, _hideCount;

    /// <summary>이 테스트 UI의 버튼만 연결합니다.</summary>
    void Awake()
    {
        _showButton.onClick.AddListener(ShowPanel);
        _hideButton.onClick.AddListener(HidePanel);
        _burstButton.onClick.AddListener(TestRepeatedShow);
        _restoreButton.onClick.AddListener(RestorePanel);
        _closeButton.onClick.AddListener(HidePanel);
    }

    /// <summary>플레이를 시작하면 등록된 등장 연출을 한 번 보여줍니다.</summary>
    void Start() { ShowPanel(); }

    /// <summary>UIDirector의 등장 함수 드롭다운에서 선택하는 함수입니다.</summary>
    public void ShowPanel()
    {
        // <UIDirector:ShowPanel>
        // 기존 본문과 인자·버튼 연결을 유지합니다. 효과는 UIDirector에서 갱신합니다.
        void __UIDirectorOriginal()
        {
            if (_panel == null) return;
            _panel.SetActive(true);
            _showCount++;
            RefreshStatus("등장 요청을 받았습니다.");
        }
        global::UIDirectorView.Dispatch(this, "ShowPanel", true, __UIDirectorOriginal);
        // </UIDirector>
    }

    /// <summary>퇴장 연출이 끝난 뒤에만 실제로 패널을 숨깁니다.</summary>
    public void HidePanel()
    {
        // <UIDirector:HidePanel>
        // 기존 본문과 인자·버튼 연결을 유지합니다. 효과는 UIDirector에서 갱신합니다.
        void __UIDirectorOriginal()
        {
            if (_panel == null) return;
            _panel.SetActive(false);
            _hideCount++;
            RefreshStatus("퇴장 연출이 끝나 패널을 숨겼습니다.");
        }
        global::UIDirectorView.Dispatch(this, "HidePanel", false, __UIDirectorOriginal);
        // </UIDirector>
    }

    /// <summary>동일 프레임에 세 번 호출하여 중복 요청 차단을 확인합니다.</summary>
    public void TestRepeatedShow()
    {
        ShowPanel(); ShowPanel(); ShowPanel();
        RefreshStatus("등장을 3회 요청했습니다. 중복 차단 중에는 처리 횟수가 한 번만 증가합니다.");
    }

    /// <summary>이 UI의 연출만 멈추고 패널과 카운터를 복구합니다. 저장된 효과는 유지합니다.</summary>
    public void RestorePanel()
    {
        var director = GetComponent<UIDirectorView>();
        if (director != null)
        {
            bool wasEnabled = director.enabled;
            director.enabled = false;
            director.enabled = wasEnabled;
        }
        _panel.SetActive(true);
        _showCount = _hideCount = 0;
        RefreshStatus("원래 모습으로 복구했습니다. 저장된 연출 설정은 유지됩니다.");
    }

    void RefreshStatus(string message)
    {
        if (_status != null) _status.text = message + $"\n등장 처리 {_showCount}회  /  퇴장 완료 {_hideCount}회";
    }

    /// <summary>이 View에서 연결한 테스트 버튼 이벤트만 해제합니다.</summary>
    void OnDestroy()
    {
        if (_showButton != null) _showButton.onClick.RemoveListener(ShowPanel);
        if (_hideButton != null) _hideButton.onClick.RemoveListener(HidePanel);
        if (_burstButton != null) _burstButton.onClick.RemoveListener(TestRepeatedShow);
        if (_restoreButton != null) _restoreButton.onClick.RemoveListener(RestorePanel);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(HidePanel);
    }
}
