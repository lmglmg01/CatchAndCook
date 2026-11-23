using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오브젝트 상호작용 뷰
/// </summary>
public class ObjectInteractView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] Image _interactIcon; // 상호작용 아이콘 이미지입니다.
    [SerializeField] TMP_Text _interactText; // 아이콘 완성 전 임시

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 오브젝트 상호작용 안내를 처음에 숨깁니다.
    /// </summary>
    private void Awake()
    {
        Hide();
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 상호작용이 가능할 때 아이콘과 안내 문구를 표시합니다.
    /// </summary>
    /// <param name="canInteractable">가능 상호작용 가능 여부입니다.</param>
    public void Show(bool canInteractable)
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (canInteractable == false)
        {
            return;
        }

        _interactIcon.enabled = canInteractable;
        _interactText.enabled = canInteractable;
    }

    /// <summary>
    /// 오브젝트 상호작용 아이콘과 안내 문구를 숨깁니다.
    /// </summary>
    public void Hide()
    {
        _interactIcon.enabled = false;
        _interactText.enabled = false;
    }

    #endregion

}
