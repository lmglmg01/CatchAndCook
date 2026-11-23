using System.Collections.Generic;
using UnityEngine;

public partial class BossView
{
    EncounterPresentation _encounterPresentation;
    readonly List<HiddenEncounterGroup> _hiddenEncounterUI = new();

    // Runtime and the Canvas Editor preview sample the same presentation.
    public sealed class EncounterPresentation
    {
        readonly RectTransform _banner;
        readonly RectTransform[] _icons = new RectTransform[3];
        readonly Vector2[] _positions = new Vector2[3];
        readonly Vector3[] _scales = new Vector3[3];
        readonly Vector2 _bannerPosition;
        readonly UnityEngine.UI.RawImage _top, _bottom;
        readonly Rect _topUV, _bottomUV;
        readonly UnityEngine.UI.Image _vignette, _dim;
        readonly Color _dimColor;

        public EncounterPresentation(GameObject notice)
        {
            var root = notice.transform;
            _banner = root.Find("WarningBanner") as RectTransform;
            _top = root.Find("TopWarningStripe")?.GetComponent<UnityEngine.UI.RawImage>();
            _bottom = root.Find("BottomWarningStripe")?.GetComponent<UnityEngine.UI.RawImage>();
            _vignette = root.Find("EncounterVignette")?.GetComponent<UnityEngine.UI.Image>();
            _dim = notice.GetComponent<UnityEngine.UI.Image>();
            if (_banner != null) _bannerPosition = _banner.anchoredPosition;
            if (_top != null) _topUV = _top.uvRect;
            if (_bottom != null) _bottomUV = _bottom.uvRect;
            if (_dim != null) _dimColor = _dim.color;
            var names = new[] { "BossIcon", "DangerLabel", "BossArriveLabel" };
            for (int i = 0; i < names.Length; i++)
            {
                _icons[i] = _banner != null ? _banner.Find(names[i]) as RectTransform : null;
                if (_icons[i] == null) continue;
                _positions[i] = _icons[i].anchoredPosition;
                _scales[i] = _icons[i].localScale;
            }
        }

        public void Sample(float elapsed, float duration)
        {
            duration = Mathf.Max(.01f, duration);
            elapsed = Mathf.Clamp(elapsed, 0f, duration);
            // Preserve the requested one second lead-in. Remaining phases fit the existing notice window.
            float lead = Mathf.Min(1f, duration * .4f);
            float rest = Mathf.Max(.01f, duration - lead);
            float p = (elapsed - lead) / rest;
            float finish = 1f - Smooth((p - .78f) / .22f);
            float envelope = Smooth(elapsed / .12f) * finish;
            float pulse = .5f - .5f * Mathf.Cos(elapsed * Mathf.PI * 2f / .62f);
            if (_vignette != null) _vignette.color = new Color(.95f, .015f, .025f, (.22f + .48f * pulse) * envelope);
            if (_dim != null) _dim.color = new Color(_dimColor.r, _dimColor.g, _dimColor.b, _dimColor.a * envelope);

            float arrowAlpha = Smooth(p / .22f) * finish;
            float scroll = Mathf.Max(0f, elapsed - lead) * .045f;
            if (_top != null)
            {
                _top.color = new Color(1, 1, 1, arrowAlpha);
                _top.uvRect = new Rect(_topUV.x - scroll, _topUV.y, _topUV.width, _topUV.height);
            }
            if (_bottom != null)
            {
                _bottom.color = new Color(1, 1, 1, arrowAlpha);
                _bottom.uvRect = new Rect(_bottomUV.x + scroll, _bottomUV.y, _bottomUV.width, _bottomUV.height);
            }
            if (_banner != null)
            {
                float enter = Mathf.Clamp01((p - .12f) / .16f);
                float exit = Mathf.Clamp01((p - .68f) / .22f);
                float x = -1550f * (1f - OutBack(enter));
                if (exit > 0f) x = -1550f * exit * exit * exit;
                _banner.anchoredPosition = _bannerPosition + Vector2.right * x;
            }
            for (int i = 0; i < _icons.Length; i++)
            {
                var icon = _icons[i];
                if (icon == null) continue;
                float age = p - (.22f + .035f * i);
                float pop = Mathf.Clamp01(age / .17f);
                icon.localScale = _scales[i] * OutBack(pop);
                float shake = age <= 0f ? 0f : Mathf.Sin(age * 105f) * 13f * Mathf.Exp(-age * 10f) * (1f - Smooth((age - .25f) / .12f));
                icon.anchoredPosition = _positions[i] + Vector2.right * shake;
            }
        }

        public void Restore()
        {
            if (_banner != null) _banner.anchoredPosition = _bannerPosition;
            for (int i = 0; i < _icons.Length; i++)
                if (_icons[i] != null) { _icons[i].anchoredPosition = _positions[i]; _icons[i].localScale = _scales[i]; }
            if (_top != null) { _top.uvRect = _topUV; _top.color = Color.white; }
            if (_bottom != null) { _bottom.uvRect = _bottomUV; _bottom.color = Color.white; }
            if (_dim != null) _dim.color = _dimColor;
            if (_vignette != null) _vignette.color = new Color(.95f, .015f, .025f, 0f);
        }

        static float Smooth(float value) { value = Mathf.Clamp01(value); return value * value * (3f - 2f * value); }
        static float OutBack(float value) { float x = value - 1f; return 1f + 2.70158f * x * x * x + 1.70158f * x * x; }
    }

    sealed class HiddenEncounterGroup
    {
        public CanvasGroup group;
        public bool added, enabled, interactable, raycasts, ignoreParents;
        public float alpha;
    }

    void HideOtherEncounterUI()
    {
        if (!Application.isPlaying || _hiddenEncounterUI.Count != 0) return;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var visited = new HashSet<Transform>();
        foreach (var canvas in canvases)
        {
            if (canvas.gameObject.scene != gameObject.scene || canvas.transform.parent != null && canvas.transform.parent.GetComponentInParent<Canvas>(true) != null) continue;
            HideOutsideNotice(canvas.transform, visited);
        }
    }

    void HideOutsideNotice(Transform node, HashSet<Transform> visited)
    {
        if (!visited.Add(node) || node == _bossNotice.transform) return;
        if (_bossNotice.transform.IsChildOf(node))
        {
            foreach (Transform child in node) HideOutsideNotice(child, visited);
            return;
        }
        var group = node.GetComponent<CanvasGroup>();
        bool added = group == null;
        if (added) group = node.gameObject.AddComponent<CanvasGroup>();
        foreach (var child in node.GetComponentsInChildren<CanvasGroup>(true))
        {
            _hiddenEncounterUI.Add(new HiddenEncounterGroup { group = child, added = child == group && added,
                enabled = child.enabled, alpha = child.alpha, interactable = child.interactable,
                raycasts = child.blocksRaycasts, ignoreParents = child.ignoreParentGroups });
            child.ignoreParentGroups = false;
        }
        group.enabled = true;
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    void RestoreEncounterUI()
    {
        for (int i = _hiddenEncounterUI.Count - 1; i >= 0; i--)
        {
            var saved = _hiddenEncounterUI[i];
            if (saved.group == null) continue;
            saved.group.alpha = saved.alpha;
            saved.group.enabled = saved.enabled;
            saved.group.interactable = saved.interactable;
            saved.group.blocksRaycasts = saved.raycasts;
            saved.group.ignoreParentGroups = saved.ignoreParents;
            if (saved.added) saved.group.enabled = false; // Allow another encounter to start safely in the same frame.
        }
        _hiddenEncounterUI.Clear();
        _encounterPresentation?.Restore();
        _approvedEncounterPresentation?.Restore();
    }
}
