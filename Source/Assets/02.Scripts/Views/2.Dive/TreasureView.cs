using TMPro;
using UnityEngine;

// The bag has no weight per item yet. Each caught item uses a configurable
// display weight until item weight data becomes available.
public class TreasureView : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float _displayWeightPerItem = 1f;
    [SerializeField, Range(0f, 1f), Tooltip("최대무게 대비 경고색을 표시할 비율입니다.")] float _weightWarningRatio = 0.9f;

    FishBag _bag;
    TMP_Text _loadText;
    TMP_Text _itemCountText;
    float _displayedCapacity = -1f;

    void Awake() => CacheUI();

    public void Bind(FishBag bag)
    {
        Unbind();
        CacheUI();
        _bag = bag;
        if (_bag != null) _bag.Changed += Refresh;
        Refresh();
    }

    public void Unbind()
    {
        if (!ReferenceEquals(_bag, null)) _bag.Changed -= Refresh;
        _bag = null;
    }

    void OnDestroy() => Unbind();

    void Update()
    {
        if (_bag != null && !Mathf.Approximately(_displayedCapacity, _bag.MaxWeight))
            Refresh();
    }

    void CacheUI()
    {
        var status = transform.Find("Status");
        if (status == null) return;
        _loadText = status.Find("CarryValue")?.GetComponent<TMP_Text>();
        _itemCountText = status.Find("BagCapacity")?.GetComponent<TMP_Text>();
    }

    public void Refresh()
    {
        if (_bag == null)
        {
            if (_loadText != null) _loadText.text = "-- / --kg";
            if (_itemCountText != null) _itemCountText.text = "";
            return;
        }

        int count = 0;
        foreach (Inventory.BagItem entry in _bag.Items)
            if (entry.ItemData != null && entry.Count > 0)
                count = entry.Count > int.MaxValue - count ? int.MaxValue : count + entry.Count;

        _displayedCapacity = _bag.MaxWeight;
        float estimatedLoad = count * Mathf.Max(0.01f, _displayWeightPerItem);
        if (_loadText != null)
            UpdateWeightText(estimatedLoad, _bag.MaxWeight);
        if (_itemCountText != null) _itemCountText.text = count + " ITEMS";
    }
    /// <summary>현재무게는 흰색, 최대무게는 남색으로 표시하며 경고 비율 이상이면 현재무게만 빨간색으로 전환합니다.</summary>
    void UpdateWeightText(float current, float maximum)
    {
        if (_loadText == null) return;
        bool warning = maximum > 0f ? current >= maximum * _weightWarningRatio : current > 0f;
        string currentColor = warning ? "FF5454" : "FDF7E4";
        _loadText.richText = true;
        _loadText.color = Color.white;
        _loadText.text = "<color=#" + currentColor + ">" + current.ToString("0.0")
            + "</color><color=#003850> / " + maximum.ToString("0.0") + "kg</color>";
    }}
