using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

sealed class TalkPortraitPreview : IDisposable
{
    const int MaxTextureSize = 768;
    const int HudTextureSize = 160;
    const float HudFrameInterval = 1f / 15f;
    const int PreviewLayer = 5; // Unity의 기본 UI 레이어
    const float StageSpacing = 128f;
    static readonly Vector3 StageOrigin = new Vector3(0f, -10000f, 0f);
    static readonly HashSet<int> ActiveStages = new HashSet<int>();

    readonly UnityEngine.UI.Image _image;
    readonly GameObject _placeholder;
    readonly bool _imageWasEnabled;
    readonly bool _placeholderWasActive;
    readonly Transform _source;
    readonly GameObject _stage;
    readonly int _stageIndex;
    readonly Transform _model;
    readonly Dictionary<Transform, Transform> _pose = new Dictionary<Transform, Transform>();
    readonly List<(Renderer Source, Renderer Preview)> _renderers = new List<(Renderer, Renderer)>();

    readonly Transform _profileHead;
    readonly float _profileTorsoLength;
    Vector3 _profileCameraOffset;
    float _nextHudFrame;
    Transform _facing;
    Camera _camera;
    RenderTexture _texture;
    UnityEngine.UI.RawImage _output;
    bool _disposed;

    TalkPortraitPreview(UnityEngine.UI.Image image, Transform source, Animator profileRig)
    {
        _image = image;
        _imageWasEnabled = image.enabled;
        _placeholder = image.transform.Find(profileRig != null ? "PortraitPlaceholder" : "IllustrationPlaceholder")?.gameObject;
        _placeholderWasActive = _placeholder != null && _placeholder.activeSelf;

        _source = source;
        _facing = source;
        _stage = new GameObject("TalkPortraitStage");
        int index = 0;
        while (!ActiveStages.Add(index))
            index++;
        _stageIndex = index;
        _stage.transform.position = StageOrigin + Vector3.right * (index * StageSpacing);

        _model = new GameObject(source.name).transform;
        _model.SetParent(_stage.transform, false);
        _model.gameObject.layer = PreviewLayer;
        _pose.Add(source, _model);
        if (profileRig != null)
        {
            Transform head = profileRig.GetBoneTransform(HumanBodyBones.Head);
            Transform chest = profileRig.GetBoneTransform(HumanBodyBones.Chest);
            _profileHead = CopyTransform(head);
            _profileTorsoLength = chest != null ? Vector3.Distance(head.position, chest.position)
                : .4f * profileRig.humanScale * Mathf.Abs(source.lossyScale.y);
        }
    }

    internal UnityEngine.UI.RawImage Output => _output;

    internal static TalkPortraitPreview Create(UnityEngine.UI.Image image, Component character, bool forHud = false)
    {
        if (image == null || character == null)
            return null;

        Vector2 size = image.rectTransform.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            return null;

        Animator profileRig = null;
        if (forHud)
        {
            foreach (var animator in character.GetComponentsInChildren<Animator>())
            {
                if (!animator.isHuman || animator.GetBoneTransform(HumanBodyBones.Head) == null) continue;
                profileRig = animator;
                break;
            }
            if (profileRig == null) return null;
        }

        var preview = new TalkPortraitPreview(image, profileRig != null ? profileRig.transform : character.transform, profileRig);
        try
        {
            if (preview.Open(size))
                return preview;
        }
        catch
        {
            preview.Dispose();
            throw;
        }

        preview.Dispose();
        return null;
    }

    bool Open(Vector2 size)
    {
        CopyRenderers();
        UpdatePose();
        Bounds bounds;
        bool forHud = _profileHead != null;
        if (forHud)
        {
            float torso = Mathf.Max(.01f, _profileTorsoLength);
            bounds = new Bounds(_profileHead.position + Vector3.up * (.4f * torso), Vector3.one * (2.65f * torso));
        }
        else if (!TryGetFramingBounds(out bounds))
            return false;

        size *= (forHud ? HudTextureSize : MaxTextureSize) / Mathf.Max(size.x, size.y);
        _texture = new RenderTexture(Mathf.Max(1, Mathf.RoundToInt(size.x)),
            Mathf.Max(1, Mathf.RoundToInt(size.y)), 24, RenderTextureFormat.ARGB32);
        _texture.name = forHud ? "PlayerProfilePortrait" : "TalkPortrait";
        if (!_texture.Create())
            return false;

        CreateCamera(bounds);
        AddKeyLight(bounds);
        ConnectImage();
        if (forHud) _profileCameraOffset = _camera.transform.position - _profileHead.position;
        _camera.enabled = !forHud;
        return true;
    }

    float _visibleBottom = -1f;

    internal void AlignBottom(RectTransform window)
    {
        if (_output == null || _texture == null || window == null) return;
        if (_visibleBottom < 0f)
        {
            var previous = RenderTexture.active;
            var sample = new Texture2D(_texture.width, _texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = _texture;
                sample.ReadPixels(new Rect(0, 0, _texture.width, _texture.height), 0, 0);
                sample.Apply();
                var pixels = sample.GetPixels32();
                for (int y = 0; y < sample.height && _visibleBottom < 0f; y++)
                    for (int x = 0; x < sample.width; x++)
                        if (pixels[y * sample.width + x].a > 32)
                        { _visibleBottom = (y + .5f) / sample.height; break; }
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(sample);
            }
        }
        if (_visibleBottom < 0f) return;
        var corners = new Vector3[4];
        window.GetWorldCorners(corners);
        var rect = _output.rectTransform;
        var bottom = rect.TransformPoint(new Vector3(rect.rect.center.x, rect.rect.yMin + rect.rect.height * _visibleBottom, 0f));
        var position = _image.rectTransform.position;
        position.y += corners[0].y - bottom.y;
        _image.rectTransform.position = position;
    }

    void ConnectImage()
    {
        var display = new GameObject("PortraitPreview", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
        var rect = (RectTransform)display.transform;
        rect.SetParent(_image.rectTransform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        bool forHud = _profileHead != null;
        rect.localScale = forHud ? Vector3.one : Vector3.one * 1.725f;
        _output = display.GetComponent<UnityEngine.UI.RawImage>();
        _output.texture = _texture;
        _output.color = forHud ? Color.white : _image.color;
        _output.raycastTarget = false;
        _output.maskable = _image.maskable;
        if (!forHud) _image.enabled = false;
        if (_placeholderWasActive)
            _placeholder.SetActive(false);
    }

    void CopyRenderers()
    {
        var properties = new MaterialPropertyBlock();
        foreach (Renderer source in _source.GetComponentsInChildren<Renderer>(true))
        {
            Renderer preview;
            if (source is SkinnedMeshRenderer skin && skin.sharedMesh != null)
            {
                preview = CopySkinnedRenderer(skin);
                if (preview == null)
                    continue;

                if (_profileHead == null && _facing == _source && skin.enabled && skin.gameObject.activeInHierarchy && !skin.forceRenderingOff)
                    _facing = skin.transform;
            }
            else if (source is MeshRenderer && source.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
            {
                GameObject copy = CopyTransform(source.transform).gameObject;
                copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                preview = copy.AddComponent<MeshRenderer>();
            }
            else
            {
                continue;
            }

            preview.sharedMaterials = source.sharedMaterials;
            preview.shadowCastingMode = ShadowCastingMode.Off;
            preview.receiveShadows = false;
            preview.lightProbeUsage = LightProbeUsage.Off;
            preview.reflectionProbeUsage = ReflectionProbeUsage.Off;
            source.GetPropertyBlock(properties);
            preview.SetPropertyBlock(properties);
            _renderers.Add((source, preview));
        }
    }

    SkinnedMeshRenderer CopySkinnedRenderer(SkinnedMeshRenderer source)
    {
        Transform[] bones = source.bones;
        foreach (Transform bone in bones)
        {
            if (bone != null && !bone.IsChildOf(_source))
                return null;
        }

        for (int i = 0; i < bones.Length; i++)
            bones[i] = bones[i] != null ? CopyTransform(bones[i]) : null;

        var preview = CopyTransform(source.transform).gameObject.AddComponent<SkinnedMeshRenderer>();
        preview.sharedMesh = source.sharedMesh;
        preview.bones = bones;
        if (source.rootBone != null && source.rootBone.IsChildOf(_source))
            preview.rootBone = CopyTransform(source.rootBone);
        preview.localBounds = source.localBounds;
        preview.quality = source.quality;
        preview.updateWhenOffscreen = true;
        return preview;
    }

    Transform CopyTransform(Transform source)
    {
        if (_pose.TryGetValue(source, out Transform copy))
            return copy;

        Transform parent = CopyTransform(source.parent);
        copy = new GameObject(source.name).transform;
        copy.SetParent(parent, false);
        copy.gameObject.layer = PreviewLayer;
        _pose.Add(source, copy);
        return copy;
    }

    internal void UpdateHud(bool visible)
    {
        bool due = visible && Time.unscaledTime >= _nextHudFrame;
        _camera.enabled = due;
        if (!due) return;
        _nextHudFrame = Time.unscaledTime + HudFrameInterval;
        UpdatePose();
        _camera.transform.position = _profileHead.position + _profileCameraOffset;
    }

    internal void UpdatePose()
    {
        if (_source == null)
            return;

        Transform facing = _facing != null ? _facing : _source;
        _model.localRotation = Quaternion.Inverse(facing.rotation) * _source.rotation;
        _model.localScale = _source.lossyScale;
        foreach (var pair in _pose)
        {
            Transform source = pair.Key;
            if (source == null || source == _source)
                continue;

            pair.Value.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            pair.Value.localScale = source.localScale;
        }

        foreach (var pair in _renderers)
        {
            Renderer source = pair.Source;
            Renderer preview = pair.Preview;
            preview.enabled = source != null && source.enabled && source.gameObject.activeInHierarchy && !source.forceRenderingOff;
            if (preview.enabled && source is SkinnedMeshRenderer skin && preview is SkinnedMeshRenderer copy)
            {
                int count = copy.sharedMesh.blendShapeCount;
                for (int i = 0; i < count; i++)
                    copy.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
            }
        }
    }

    bool TryGetFramingBounds(out Bounds bounds)
    {
        // 현재 자세의 메시 크기로 카메라 구도만 맞춥니다.
        var mesh = new Mesh();
        try
        {
            bounds = default;
            bool hasBounds = false;
            foreach (var pair in _renderers)
            {
                if (!pair.Preview.enabled)
                    continue;

                Bounds current = pair.Preview.bounds;
                if (pair.Source is SkinnedMeshRenderer skin)
                {
                    skin.BakeMesh(mesh);
                    var matrix = pair.Preview.localToWorldMatrix;
                    Vector3 extents = mesh.bounds.extents;
                    Vector3 x = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
                    Vector3 y = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
                    Vector3 z = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
                    var size = new Vector3(
                        Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                        Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                        Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2f;
                    current = new Bounds(matrix.MultiplyPoint3x4(mesh.bounds.center), size);
                }

                if (hasBounds)
                    bounds.Encapsulate(current);
                else
                    bounds = current;
                hasBounds = true;
            }

            return hasBounds && bounds.size.sqrMagnitude > 0f;
        }
        finally
        {
            UnityEngine.Object.Destroy(mesh);
        }
    }

    void CreateCamera(Bounds bounds)
    {
        _camera = new GameObject("PortraitCamera").AddComponent<Camera>();
        _camera.enabled = false;
        _camera.transform.SetParent(_stage.transform, false);
        _camera.targetTexture = _texture;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Color.clear;
        _camera.cullingMask = 1 << PreviewLayer;
        _camera.useOcclusionCulling = false;
        _camera.allowHDR = false;
        _camera.orthographic = true;
        _camera.aspect = (float)_texture.width / _texture.height;
        _camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / _camera.aspect) * 1.12f;

        float distance = Mathf.Max(2f, bounds.extents.magnitude * 3f);
        _camera.nearClipPlane = 0.05f;
        _camera.farClipPlane = distance * 2f;
        _camera.transform.SetPositionAndRotation(bounds.center + Vector3.forward * distance,
            Quaternion.LookRotation(Vector3.back));

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
        {
            var data = _camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.volumeLayerMask = 0;
        }
    }

    void AddKeyLight(Bounds bounds)
    {
        var light = new GameObject("PortraitLight").AddComponent<Light>();
        light.transform.SetParent(_stage.transform, false);
        float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
        light.transform.position = bounds.center + new Vector3(-0.6f, 0.75f, 1.2f) * radius;
        light.type = LightType.Point;
        light.cullingMask = 1 << PreviewLayer;
        light.range = radius * 4f;
        light.intensity = radius * radius * 1.8f;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_camera != null)
        {
            _camera.enabled = false;
            _camera.targetTexture = null;
        }

        if (_stage != null)
        {
            _stage.SetActive(false);
            UnityEngine.Object.Destroy(_stage);
        }

        ActiveStages.Remove(_stageIndex);

        if (_output != null)
        {
            _output.enabled = false;
            _output.texture = null;
            UnityEngine.Object.Destroy(_output.gameObject);
        }

        if (_texture != null)
        {
            _texture.Release();
            UnityEngine.Object.Destroy(_texture);
        }

        if (_image != null)
            _image.enabled = _imageWasEnabled;
        if (_placeholderWasActive && _placeholder != null)
            _placeholder.SetActive(true);
    }
}
