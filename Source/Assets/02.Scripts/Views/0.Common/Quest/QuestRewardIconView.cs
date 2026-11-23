using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>보상 아이콘과 대체 이미지를 표시하고 마우스를 올렸을 때 툴팁을 요청합니다.</summary>
[DisallowMultipleComponent]
public class QuestRewardIconView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private QuestListView.Reward _reward;
    private QuestRewardTooltipView _tooltip;
    private UnityEngine.UI.Image _image;

    /// <summary>기존 아이콘에 마우스 처리만 추가하므로 프리팹을 다시 연결할 필요가 없습니다.</summary>
    public static void Configure(UnityEngine.UI.Image image, QuestListView.Reward reward, Sprite fallback)
    {
        if (image == null) return;
        var view = image.GetComponent<QuestRewardIconView>();
        if (view == null) view = image.gameObject.AddComponent<QuestRewardIconView>();
        view.Bind(reward, fallback);
    }

    /// <summary>미설정 보상이나 이미지가 없는 보상은 살짝 어두운 동전으로 표시합니다.</summary>
    private void Bind(QuestListView.Reward reward, Sprite fallback)
    {
        _reward = reward;
        if (_image == null) _image = GetComponent<UnityEngine.UI.Image>();
        bool configured = QuestRewardPresentation.IsConfigured(reward);
        bool originalImage = configured && reward.icon != null;
        _image.overrideSprite = null;
        _image.sprite = originalImage ? reward.icon : fallback;
        _image.enabled = _image.sprite != null;
        _image.color = originalImage ? Color.white : new Color(.65f, .65f, .65f, 1f);
        _image.preserveAspect = true;
        _image.raycastTarget = true;
        var screen = GetComponentInParent<QuestScreenView>(true);
        if (screen != null)
        {
            _tooltip = screen.GetComponent<QuestRewardTooltipView>();
            if (_tooltip == null) _tooltip = screen.gameObject.AddComponent<QuestRewardTooltipView>();
        }
        if (_tooltip != null && _tooltip.IsShowing(this))
            _tooltip.Show(this, QuestRewardPresentation.GetTooltip(_reward));
    }

    /// <summary>마우스가 올라오면 확정 여부에 맞는 보상 정보를 표시합니다.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltip != null) _tooltip.Show(this, QuestRewardPresentation.GetTooltip(_reward));
    }

    /// <summary>마우스가 벗어나면 이 아이콘이 열었던 툴팁만 닫습니다.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (_tooltip != null) _tooltip.Hide(this);
    }

    /// <summary>스크롤·탭 전환·목록 갱신으로 아이콘이 숨겨질 때 툴팁도 닫습니다.</summary>
    private void OnDisable()
    {
        if (_tooltip != null) _tooltip.Hide(this);
    }
}