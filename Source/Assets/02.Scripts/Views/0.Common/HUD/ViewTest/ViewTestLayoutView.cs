using UnityEngine;

// 이 컴포넌트는 ViewTest 씬 인스턴스에만 연결합니다. 기존 View의 API와 동작을 변경하지 않습니다.
[DefaultExecutionOrder(1000)]
public class ViewTestLayoutView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] RectTransform _phone; // 스마트폰 값입니다.
    [SerializeField] GameObject[] _boardOnly; // 선상 전용 목록입니다.
    [SerializeField] CanvasGroup[] _boardGroups; // 선상 그룹 목록입니다.

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 초기화가 끝난 뒤 시작 표시를 적용합니다.
    /// </summary>
    void Start()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (gameObject.scene.name != "ViewTest")
        {
            enabled = false;
            return;
        }
    // 표시 여부는 BoardView의 상호작용 API가 관리합니다. 시작 시 강제로 활성화하지 않습니다.
    }

    /// <summary>
    /// 다른 갱신이 끝난 뒤 화면 표시를 맞춥니다.
    /// </summary>
    void LateUpdate()
    {
        bool open = false; // 열기 여부입니다.
        if (_phone != null && _phone.gameObject.activeInHierarchy)
        {
            var canvas = _phone.GetComponentInParent<Canvas>(); // 캔버스 값입니다.
            var rect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null; // 사각 영역 값입니다.
            if (rect != null)
            {
                // 아래쪽 슬라이드에서도 화면 밖의 폰을 열린 상태로 오인하지 않습니다.
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, _phone);
                open = bounds.max.y > rect.rect.yMin && bounds.min.y < rect.rect.yMax && bounds.max.x > rect.rect.xMin && bounds.min.x < rect.rect.xMax;
            }
        }

        // group: 그룹 값입니다.
        foreach (var group in _boardGroups)
        {
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (group == null)
            {
                continue;
            }

            group.alpha = open ? 0f : 1f;
            group.interactable = !open;
            group.blocksRaycasts = !open;
        }
    }

    #endregion

}
