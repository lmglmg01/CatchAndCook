using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>초밥 Canvas와 같은 씬의 공통 HUD에만 식당용 색상 리소스를 적용합니다.</summary>
[DisallowMultipleComponent]
public sealed class SushiHudThemeView : MonoBehaviour
{
    [System.Serializable]
    public struct Entry { public string Path; public Sprite Sprite; }
    [SerializeField] Entry[] _entries;
    [SerializeField] Sprite _coinIcon;
    struct Original { public Image Image; public Sprite Sprite; public Color Color; public bool Enabled; }
    readonly List<Original> _originals = new();

    RectTransform _goldBackground, _goldIcon;
    Vector2 _goldPosition, _goldSize, _coinPosition, _coinSize;

    void OnEnable() => Apply();
    void Start() => Apply();

    void Apply()
    {
        BaseView target = null;
        foreach (var view in FindObjectsByType<BaseView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (view.gameObject.scene == gameObject.scene) { target = view; break; }
        if (target == null) return;
        var canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        if (_entries != null) foreach (var entry in _entries)
        {
            var image = canvas.transform.Find(entry.Path)?.GetComponent<Image>();
            if (image != null && entry.Sprite != null) Set(image, entry.Sprite);
        }
        var coin = canvas.transform.Find("OwnGoldWindow/Background/GoldIcon")?.GetComponent<Image>();
        if (coin != null && _coinIcon != null) Set(coin, _coinIcon, true);
        var background=canvas.transform.Find("OwnGoldWindow/Background") as RectTransform;
        if(background!=null)
        {
            if(_goldBackground!=background){_goldBackground=background;_goldPosition=background.anchoredPosition;_goldSize=background.sizeDelta;}
            background.anchoredPosition=_goldPosition+new Vector2(2,-12);
            background.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,184);background.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,60);
        }
        if(coin!=null)
        {
            var r=coin.rectTransform;
            if(_goldIcon!=r){_goldIcon=r;_coinPosition=r.anchoredPosition;_coinSize=r.sizeDelta;}
            r.anchoredPosition=new Vector2(-94,6);r.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,80);r.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,80);
        }

    }

    void Set(Image image, Sprite sprite, bool enable = false)
    {
        if (!_originals.Exists(state => state.Image == image))
            _originals.Add(new Original { Image = image, Sprite = image.sprite, Color = image.color, Enabled = image.enabled });
        image.sprite = sprite;
        image.color = Color.white;
        if (enable) image.enabled = true;
    }

    void OnDisable()
    {
        foreach (var state in _originals) if (state.Image != null)
        { state.Image.sprite = state.Sprite; state.Image.color = state.Color; state.Image.enabled = state.Enabled; }
        _originals.Clear();
        if(_goldBackground!=null){_goldBackground.anchoredPosition=_goldPosition;_goldBackground.sizeDelta=_goldSize;}
        if(_goldIcon!=null){_goldIcon.anchoredPosition=_coinPosition;_goldIcon.sizeDelta=_coinSize;}
        _goldBackground=null;_goldIcon=null;
    }
}
