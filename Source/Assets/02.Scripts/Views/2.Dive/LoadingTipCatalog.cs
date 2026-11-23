using System;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/Loading Tips")]
public sealed class LoadingTipCatalog : ScriptableObject
{
    [Serializable]
    public struct Tip
    {
        public Sprite Icon;
        public string Title;
        [TextArea] public string Description;
    }

    [SerializeField] Tip[] _tips = Array.Empty<Tip>();

    static readonly System.Random Random = new System.Random();
    [NonSerialized] int _lastTip = -1;

    public bool TryPick(out Tip tip)
    {
        int count = _tips.Length;
        if (count == 0)
        {
            tip = default;
            return false;
        }

        bool excludeLast = count > 1 && _lastTip >= 0 && _lastTip < count;
        int index = Random.Next(excludeLast ? count - 1 : count);
        if (excludeLast && index >= _lastTip) index++;

        _lastTip = index;
        tip = _tips[index];
        return true;
    }
}
