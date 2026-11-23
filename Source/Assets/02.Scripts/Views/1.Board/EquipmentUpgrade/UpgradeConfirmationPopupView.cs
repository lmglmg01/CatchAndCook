using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>강화 여부를 확인하고, 확인한 경우에만 전달받은 기존 강화 동작을 실행합니다.</summary>
public class UpgradeConfirmationPopupView : MonoBehaviour
{
    [SerializeField] GameObject _overlay;
    [SerializeField] TMP_Text _gold, _cost;
    [SerializeField] UnityEngine.UI.Button _cancel, _confirm;
    [SerializeField] Sprite _closeIcon;
    [SerializeField] Sprite _goldBackgroundSprite;
    Action _onConfirm;
    GameObject _previousSelection;
    public bool IsOpen => _overlay != null && _overlay.activeSelf;

    /// <summary>취소와 확인 버튼에 각 동작을 한 번 연결합니다.</summary>
    void Awake() {
        UIEscapeView.Register(this, _cancel); _cancel.onClick.AddListener(Close); _confirm.onClick.AddListener(Confirm); ApplyLayout(); }

    /// <summary>취소·확인 버튼과 재화 참조는 유지하고 새 안내창 배치만 적용합니다.</summary>
    public void ApplyLayout()
    {
        if (_overlay == null) return;
        var panel = _overlay.transform.Find("Panel");
        if (panel == null) return;
        Place(panel, 0, 0, 730, 450);
        var panelImage=panel.GetComponent<UnityEngine.UI.Image>();if(panelImage!=null)panelImage.color=new Color(1f,1f,1f,.98f);
        Place(panel.Find("Title"), 0, 155, 500, 60);
        var title=panel.Find("Title").GetComponent<TMP_Text>();title.fontSize=40;title.enableAutoSizing=false;
        Place(panel.Find("MessageBackground"), 0, 25, 630, 60);
        var messageBackground = panel.Find("MessageBackground")?.GetComponent<UnityEngine.UI.Image>();
        if (messageBackground != null) { messageBackground.sprite = null; messageBackground.enabled = false; }
        var message = panel.Find("MessageBackground")?.GetComponentInChildren<TMP_Text>(true);
        if (message != null) { Place(message.transform, 0, 0, 630, 60); message.fontSize = 30; message.enableAutoSizing = false; }
        var owned = panel.Find("OwnedGold");
        if (owned != null) owned.gameObject.SetActive(false);
        var cost = panel.Find("Cost");
        Place(cost, -135, -128, 260, 120);
        if (cost != null)
        {
            Place(cost.Find("Title"), 0, 52, 260, 45);
            // Canvas_Base의 동전 크기와 배치 비율을 따르며 배경은 안내창과 구분합니다.
            var background = cost.Find("Background");
            Place(background, 19.5f, -14, 185, 50);
            var goldImage = background != null ? background.GetComponent<UnityEngine.UI.Image>() : null;
            if (goldImage != null && _goldBackgroundSprite != null)
            {
                goldImage.sprite = _goldBackgroundSprite;
                goldImage.type = UnityEngine.UI.Image.Type.Sliced;
                goldImage.color = Color.white;
                goldImage.preserveAspect = false;
                var separateIcon = cost.Find("Icon");
                if (separateIcon != null) { separateIcon.gameObject.SetActive(true); Place(separateIcon, -73, -14, 78, 78); separateIcon.SetAsLastSibling(); }
            }
            Place(_cost != null ? _cost.transform : null, 8.5f, -3, 140, 58);
            var label = cost.Find("Title")?.GetComponent<TMP_Text>();
            if (label != null) { label.font = _cost.font; label.fontSize = 28; label.fontStyle = FontStyles.Normal; label.alignment = TextAlignmentOptions.Center; }
        }
        if (_cost != null) { _cost.fontSize = 32; _cost.enableAutoSizing = false; _cost.alignment = TextAlignmentOptions.Center; }
        Place(_confirm.transform, 142, -131, 240, 88);
        _confirm.transition=UnityEngine.UI.Selectable.Transition.None;_confirm.targetGraphic.canvasRenderer.SetColor(Color.white);
        foreach (var text in _confirm.GetComponentsInChildren<TMP_Text>(true)) { Place(text.transform,0,5,240,78);text.fontSize = 40; text.enableAutoSizing = false; }
        Place(_cancel.transform, 296, 156, 49, 49);
        var image = _cancel.GetComponent<UnityEngine.UI.Image>();
        if (image != null && _closeIcon != null)
        {
            image.sprite = _closeIcon; image.type = UnityEngine.UI.Image.Type.Simple;
            // 흰색 X 원화의 투명 여백을 포함해 보이는 크기 약 36px와 우측 여백 50px를 맞춥니다.
            image.color = new Color32(253,247,228,255);
            var outline=image.GetComponent<UnityEngine.UI.Outline>()??image.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor=image.color;outline.effectDistance=new Vector2(2.5f,-2.5f);outline.useGraphicAlpha=true;
            _cancel.transition = UnityEngine.UI.Selectable.Transition.None; image.canvasRenderer.SetColor(Color.white);
            foreach (var text in _cancel.GetComponentsInChildren<TMP_Text>(true)) text.gameObject.SetActive(false);
        }
    }

    /// <summary>중앙 기준으로 기존 참조를 유지한 채 위치와 크기를 갱신합니다.</summary>
    static void Place(Transform target, float x, float y, float width, float height)
    {
        if (!(target is RectTransform rect)) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
    }

    /// <summary>현재 재화와 비용을 표시하고 확인할 강화 요청을 보관합니다.</summary>
    public void Show(float gold, bool knownGold, float cost, Action onConfirm)
    {
        if (IsOpen) return;
        _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        _gold.text = knownGold ? gold.ToString("N0") : "—";
        _cost.text = cost.ToString("N0");
        _onConfirm = onConfirm;
        _overlay.SetActive(true);
        _overlay.transform.SetAsLastSibling();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_cancel.gameObject);
    }

    /// <summary>요청을 먼저 비워 중복 입력을 막은 뒤 기존 강화 동작을 실행합니다.</summary>
    void Confirm()
    {
        if (!IsOpen) return;
        var callback = _onConfirm;
        Close();
        callback?.Invoke();
    }

    /// <summary>강화를 실행하지 않고 창을 닫으며 이전 선택을 복원합니다.</summary>
    public void Close()
    {
        _onConfirm = null;
        if (!IsOpen) return;
        _overlay.SetActive(false);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
        _previousSelection = null;
    }

    /// <summary>화면이 닫히면 보관한 요청도 취소합니다.</summary>
    void OnDisable() { Close(); }

    /// <summary>이 컴포넌트에서 연결한 버튼 이벤트만 해제합니다.</summary>
    void OnDestroy()
    {
        if (_cancel != null) _cancel.onClick.RemoveListener(Close);
        if (_confirm != null) _confirm.onClick.RemoveListener(Confirm);
    }
}



