using System.Collections.Generic;
using UnityEngine;

public partial class BossView
{
    [SerializeField, Tooltip("Boss 씬에서 승인한 두 번째 점멸 강타 연출을 사용합니다.")]
    bool _useApprovedEncounter;
    ApprovedEncounterPresentation _approvedEncounterPresentation;

    void SampleEncounterPresentation(float elapsed, float duration)
    {
        if (_useApprovedEncounter)
        {
            _approvedEncounterPresentation ??= new ApprovedEncounterPresentation(_bossNotice);
            _approvedEncounterPresentation.Sample(elapsed, duration);
        }
        else
        {
            _encounterPresentation ??= new EncounterPresentation(_bossNotice);
            _encounterPresentation.Sample(elapsed, duration);
        }
    }

    sealed class ApprovedEncounterPresentation
    {
        const float ApprovedDuration = 3.804f;
        readonly float _impactTimeScale = 2f, _impactScatter = 240f, _impactBaseDuration = 2.8f;
        readonly Canvas _canvas;
        readonly RectTransform _impactNotice, _impactBanner, _impactArrive;
        readonly Vector2 _impactBannerPosition;
        readonly RectTransform _impactTopStripe, _impactBottomStripe;
        readonly Color _impactBaseDimColor;
        readonly EncounterPresentation _impactPresentation;
        readonly UnityEngine.UI.Image _fragmentImage;
        readonly bool _fragmentWasEnabled;
        readonly StripePose _topPose, _bottomPose;
        bool _restored;
        Vector2 _impactFragmentCenter;
        const float BossBoardAngle = 14.999622f;
        readonly List<RectTransform> _impactPieces = new();
        readonly List<Vector2> _impactPieceOrigins = new();
        readonly RectTransform[] _impactPopImages = new RectTransform[3];
        readonly Vector2[] _impactPopPositions = new Vector2[3];
        readonly Vector3[] _impactPopScales = new Vector3[3];
        readonly List<RectTransform> _impactSurfacePieces = new();
        readonly List<Vector2> _impactSurfaceOrigins = new();
        Canvas _impactFragmentLayer;
        readonly List<Canvas> _impactLetterLayers = new();
        float ImpactAdvance => 1.796f;

        public ApprovedEncounterPresentation(GameObject notice)
        {
            _canvas = notice.GetComponentInParent<Canvas>(true);
            _impactNotice = notice.transform as RectTransform;
            _impactBanner = notice.transform.Find("WarningBanner") as RectTransform;
            _impactArrive = _impactBanner.Find("BossArriveLabel") as RectTransform;
            _impactBannerPosition = _impactBanner.anchoredPosition;
            _impactTopStripe = notice.transform.Find("TopWarningStripe") as RectTransform;
            _impactBottomStripe = notice.transform.Find("BottomWarningStripe") as RectTransform;
            _topPose = new StripePose(_impactTopStripe);
            _bottomPose = new StripePose(_impactBottomStripe);
            _impactBaseDimColor = notice.GetComponent<UnityEngine.UI.Image>().color;
            _impactPresentation = new EncounterPresentation(notice);
            _fragmentImage = _impactBanner.Find("Fragments").GetComponent<UnityEngine.UI.Image>();
            _fragmentWasEnabled = _fragmentImage.enabled;
            var names = new[] { "BossIcon", "DangerLabel", "BossArriveLabel" };
            for (int i = 0; i < names.Length; i++)
            {
                _impactPopImages[i] = _impactBanner.Find(names[i]) as RectTransform;
                _impactPopPositions[i] = _impactPopImages[i].anchoredPosition;
                _impactPopScales[i] = _impactPopImages[i].localScale;
            }
            FitImpactStripe(_impactTopStripe, true);
            FitImpactStripe(_impactBottomStripe, false);
            MakeImpactPieces();
            ConfigureImpactLayers();
        }

        public void Sample(float elapsed, float duration)
        {
            if (_restored)
            {
                FitImpactStripe(_impactTopStripe, true);
                FitImpactStripe(_impactBottomStripe, false);
                UpdateImpactLayerOrder();
                _restored = false;
            }
            SampleImpact(Mathf.Clamp(elapsed, 0f, duration) * ApprovedDuration / Mathf.Max(.01f, duration));
        }

        public void Restore()
        {
            _impactPresentation.Restore();
            _topPose.Restore(_impactTopStripe);
            _bottomPose.Restore(_impactBottomStripe);
            _fragmentImage.enabled = _fragmentWasEnabled;
            foreach (var piece in _impactPieces) if (piece != null) piece.GetComponent<UnityEngine.UI.RawImage>().color = Color.clear;
            foreach (var piece in _impactSurfacePieces) if (piece != null) piece.GetComponent<UnityEngine.UI.RawImage>().color = Color.clear;
            _restored = true;
        }

        readonly struct StripePose
        {
            readonly Vector2 _pivot, _size, _position;
            readonly Vector3 _scale;
            readonly Quaternion _rotation;
            public StripePose(RectTransform rect)
            {
                _pivot = rect.pivot; _size = rect.sizeDelta; _position = rect.anchoredPosition;
                _scale = rect.localScale; _rotation = rect.localRotation;
            }
            public void Restore(RectTransform rect)
            {
                rect.pivot = _pivot; rect.sizeDelta = _size; rect.localScale = _scale;
                rect.localRotation = _rotation; rect.anchoredPosition = _position;
            }
        }

    void FitImpactStripe(RectTransform stripe, bool top)
        {
            float angle = 2f * Mathf.Deg2Rad;
            float width = _impactNotice.rect.width;
            float edgeMargin = 12f;
            float minimumVisible = 32f;
            float zoom = Mathf.Max(1.25f, (width * Mathf.Tan(angle) + edgeMargin + minimumVisible) * Mathf.Cos(angle) / stripe.rect.height);
            stripe.pivot = new Vector2(.5f, .5f);
            stripe.sizeDelta += new Vector2(160f, 0f);
            stripe.localScale *= zoom;
            stripe.localRotation = Quaternion.Euler(0, 0, -2f);
            float halfThickness = stripe.rect.height * stripe.localScale.y / (2f * Mathf.Cos(angle));
            float halfSlope = width * .5f * Mathf.Tan(angle);
            float inset = halfThickness - halfSlope - edgeMargin;
            stripe.anchoredPosition = new Vector2(0f, top ? -inset : inset);
        }

    void ConfigureImpactLayers()
        {
            var board = _impactBanner.Find("NoticeBoard");
            var fragments = _impactBanner.Find("Fragments");
            board.SetAsFirstSibling();
            fragments.SetSiblingIndex(board.GetSiblingIndex() + 1);
            _impactFragmentLayer = fragments.GetComponent<Canvas>();
            if (_impactFragmentLayer == null)
                _impactFragmentLayer = fragments.gameObject.AddComponent<Canvas>();
            _impactLetterLayers.Clear();
            foreach (string name in new[] { "BossIcon", "DangerLabel", "BossArriveLabel" })
            {
                var item = _impactBanner.Find(name);
                item.SetAsLastSibling();
                var layer = item.GetComponent<Canvas>();
                if (layer == null) layer = item.gameObject.AddComponent<Canvas>();
                _impactLetterLayers.Add(layer);
            }
            UpdateImpactLayerOrder();
        }

    void UpdateImpactLayerOrder()
        {
            if (_impactFragmentLayer == null || _canvas == null) return;
            _impactFragmentLayer.overrideSorting = true;
            _impactFragmentLayer.sortingLayerID = _canvas.sortingLayerID;
            _impactFragmentLayer.sortingOrder = _canvas.sortingOrder + 1;
            foreach (var layer in _impactLetterLayers)
            {
                layer.overrideSorting = true;
                layer.sortingLayerID = _canvas.sortingLayerID;
                layer.sortingOrder = _canvas.sortingOrder + 2;
            }
        }

    void MakeImpactPieces()
        {
            var original = _impactBanner.Find("Fragments").GetComponent<UnityEngine.UI.Image>();
            if (original.sprite == null) return;
            original.enabled = false;
            var rect = original.rectTransform.rect;
            _impactFragmentCenter = rect.center;
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(original.sprite);
            const int cols = 5, rows = 3;
            for (int y = 0; y < rows; y++) for (int x = 0; x < cols; x++)
            {
                var go = new GameObject($"EncounterShard_{x}_{y}", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
                go.hideFlags = HideFlags.DontSave;
                var piece = (RectTransform)go.transform;
                piece.SetParent(original.transform, false);
                piece.anchorMin = piece.anchorMax = original.rectTransform.pivot;
                piece.sizeDelta = new Vector2(rect.width / cols, rect.height / rows);
                var origin = new Vector2(rect.xMin + (x + .5f) * rect.width / cols, rect.yMin + (y + .5f) * rect.height / rows);
                piece.anchoredPosition = origin;
                var raw = go.GetComponent<UnityEngine.UI.RawImage>();
                raw.texture = original.sprite.texture;
                raw.uvRect = new Rect(Mathf.Lerp(uv.x, uv.z, (float)x / cols), Mathf.Lerp(uv.y, uv.w, (float)y / rows), (uv.z - uv.x) / cols, (uv.w - uv.y) / rows);
                raw.raycastTarget = false;
                _impactPieces.Add(piece); _impactPieceOrigins.Add(origin);
            }
            _impactSurfacePieces.Clear();
            _impactSurfaceOrigins.Clear();
            var crops = new[] { new Rect(613, 149, 190, 85), new Rect(601, 218, 103, 72), new Rect(638, 0, 99, 51) };
            var centers = new[] { new Vector2(365, -315), new Vector2(575, -260), new Vector2(840, -110), new Vector2(955, -65), new Vector2(1040, -55) };
            var sizes = new[] { new Vector2(44, 28), new Vector2(38, 30), new Vector2(52, 24), new Vector2(60, 34), new Vector2(38, 26) };
            for (int i = 0; i < centers.Length; i++)
            {
                var go = new GameObject($"EncounterSurfaceShard_{i}", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
                go.hideFlags = HideFlags.DontSave;
                var piece = (RectTransform)go.transform;
                piece.SetParent(original.transform, false);
                piece.anchorMin = piece.anchorMax = original.rectTransform.pivot;
                piece.sizeDelta = sizes[i];
                var origin = centers[i] - original.rectTransform.anchoredPosition;
                piece.anchoredPosition = origin;
                var crop = crops[i % crops.Length];
                var raw = go.GetComponent<UnityEngine.UI.RawImage>();
                raw.texture = original.sprite.texture;
                raw.uvRect = new Rect(Mathf.Lerp(uv.x, uv.z, crop.x / 803f), Mathf.Lerp(uv.y, uv.w, 1f - crop.yMax / 448f), (uv.z - uv.x) * crop.width / 803f, (uv.w - uv.y) * crop.height / 448f);
                raw.raycastTarget = false;
                _impactSurfacePieces.Add(piece);
                _impactSurfaceOrigins.Add(origin);
            }
        }

    void SampleImpact(float elapsed)
        {
            _fragmentImage.enabled = false;
            float actualSourceTime = elapsed / _impactTimeScale;
            float sourceTime = (elapsed + ImpactAdvance) / _impactTimeScale;
            _impactPresentation.Sample(sourceTime, _impactBaseDuration);
            var dim = _impactNotice.GetComponent<UnityEngine.UI.Image>();
            float fadeIn = Mathf.Clamp01(actualSourceTime / .12f);
            fadeIn = fadeIn * fadeIn * (3f - 2f * fadeIn);
            float envelope = fadeIn * (_impactBaseDimColor.a > 0 ? dim.color.a / _impactBaseDimColor.a : 0);
            float pulse = .5f - .5f * Mathf.Cos(actualSourceTime * Mathf.PI * 2f / .62f);
            dim.color = new Color(_impactBaseDimColor.r, _impactBaseDimColor.g, _impactBaseDimColor.b, _impactBaseDimColor.a * envelope);
            _impactNotice.Find("EncounterVignette").GetComponent<UnityEngine.UI.Image>().color = new Color(.95f, .015f, .025f, (.22f + .48f * pulse) * envelope);
            float lead = Mathf.Min(1f, _impactBaseDuration * .4f);
            float rest = Mathf.Max(.01f, _impactBaseDuration - lead);
            float p = (sourceTime - lead) / rest;
            float land = Mathf.Clamp01((p - .34f) / .12f);
            float travel = land * land * land;
            var directionIn = new Vector2(Mathf.Cos(BossBoardAngle * Mathf.Deg2Rad), Mathf.Sin(BossBoardAngle * Mathf.Deg2Rad));
            float exit = Mathf.Clamp01((p - .68f) / .22f);
            _impactBanner.anchoredPosition = _impactBannerPosition - directionIn * (2300f * (1f - travel));
            if (exit > 0f) _impactBanner.anchoredPosition -= directionIn * (2300f * exit * exit * exit);
            for (int i = 0; i < _impactPopImages.Length; i++)
            {
                float age = p - (.39f + .035f * i);
                float pop = Mathf.Clamp01(age / .17f);
                float x = pop - 1f;
                float bounce = 1f + 2.70158f * x * x * x + 1.70158f * x * x;
                _impactPopImages[i].localScale = _impactPopScales[i] * bounce;
                float shake = age <= 0f ? 0f : Mathf.Sin(age * 105f) * 13f * Mathf.Exp(-age * 10f) * (1f - Mathf.SmoothStep(0, 1, (age - .25f) / .12f));
                _impactPopImages[i].anchoredPosition = _impactPopPositions[i] + Vector2.right * shake;
            }
            var arriveImage = _impactArrive.GetComponent<UnityEngine.UI.Image>();
            arriveImage.color = Color.white;
            float impactAge = (elapsed - .93f * _impactTimeScale) / _impactTimeScale;
            if (impactAge >= 0)
            {
                float shake = Mathf.Sin(impactAge * 95f) * 16f * Mathf.Exp(-impactAge * 16f);
                _impactBanner.anchoredPosition += directionIn * shake;
            }
            for (int i = 0; i < _impactPieces.Count; i++)
            {
                var piece = _impactPieces[i];
                var origin = _impactPieceOrigins[i];
                float t = Mathf.Max(0, impactAge);
                float spread = 24f + _impactScatter * (1f - Mathf.Exp(-9f * t));
                var relative = origin - _impactFragmentCenter;
                var direction = new Vector2(relative.x / 300f, relative.y / 110f + .18f).normalized;
                if (direction.sqrMagnitude < .01f) direction = Vector2.up;
                piece.anchoredPosition = origin + direction * spread + Vector2.down * (160f * t * t);
                piece.localEulerAngles = new Vector3(0, 0, (i % 2 == 0 ? 1 : -1) * (35 + i * 5) * t);
                piece.localScale = Vector3.one;
                float alpha = impactAge < 0 ? 0 : 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.28f, .85f, t));
                piece.GetComponent<UnityEngine.UI.RawImage>().color = new Color(1, 1, 1, alpha);
            }
            for (int i = 0; i < _impactSurfacePieces.Count; i++)
            {
                var piece = _impactSurfacePieces[i];
                float t = Mathf.Max(0, impactAge);
                float flight = 1f - Mathf.Exp(-5f * t);
                var drift = new Vector2(i % 2 == 0 ? -25f : 35f, 18f + i * 5f);
                piece.anchoredPosition = _impactSurfaceOrigins[i] + drift * flight + Vector2.down * (25f * t * t);
                piece.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 8f + i) * 18f, (i % 2 == 0 ? -1f : 1f) * (20f + 90f * t));
                piece.localScale = Vector3.one * (1f + .35f * Mathf.Sin(Mathf.Clamp01(t / .45f) * Mathf.PI));
                float alpha = impactAge < 0 ? 0 : 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.18f, .65f, t));
                piece.GetComponent<UnityEngine.UI.RawImage>().color = new Color(1, 1, 1, alpha);
            }
        }
    }
}
