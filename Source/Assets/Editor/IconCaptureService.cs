using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ItemIconTools
{
    public enum IconLighting
    {
        Soft = 0,
        Bright = 1,
        // Saved Unlit presets use value 2.
        [InspectorName("Lit (그림자 없음)")] LitNoShadows = 2
    }
    public enum IconOverwrite { Skip, Overwrite, UniqueName }

    [Serializable]
    public class IconCaptureSettings
    {
        public int resolution;
        public string outputFolder;
        public float fill;
        public Vector3 cameraAngles;
        public bool orthographic;
        public IconLighting lighting;
        public bool shadows;
        public bool removeGloss;
        public bool blackSilhouette;
        public IconOverwrite overwrite;
        public FilterMode filter;
        public TextureImporterCompression compression;

        public bool RenderShadows => lighting != IconLighting.LitNoShadows && shadows;

        public static IconCaptureSettings Defaults() => new IconCaptureSettings
        {
            resolution = 256, outputFolder = "Assets/Generated/ItemIcons", fill = 0.8f,
            cameraAngles = new Vector3(15f, 150f, 0f), orthographic = true,
            lighting = IconLighting.LitNoShadows, overwrite = IconOverwrite.Skip,
            filter = FilterMode.Bilinear, compression = TextureImporterCompression.Uncompressed
        };
    }

    [Serializable]
    public class IconCaptureEntry
    {
        public string guid;
        public bool selected;
        public bool autoFit;
        public float scale;
        public Vector2 offset;
        public Vector3 rotation;
        public string status;
        public string output;
        public GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        public void Reset()
        {
            autoFit = true; scale = 1f; offset = Vector2.zero; rotation = Vector3.zero;
        }
    }

    public sealed class IconCaptureResult : IDisposable
    {
        public Texture2D texture;
        public float occupancy;
        public bool touchesEdge;
        public void Dispose() { if (texture != null) UnityEngine.Object.DestroyImmediate(texture); }
    }

    public static class IconCaptureService
    {
        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        static List<Renderer> Renderers(GameObject root)
        {
            var result = new List<Renderer>();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                if (r.enabled && r.gameObject.activeInHierarchy
                    && (r is MeshRenderer || r is SkinnedMeshRenderer)) result.Add(r);
            if (result.Count == 0) throw new InvalidOperationException("활성 Mesh/SkinnedMesh Renderer가 없습니다.");
            return result;
        }

        public static void ProjectBounds(IEnumerable<Renderer> renderers, Quaternion rotation,
            out Vector3 min, out Vector3 max)
        {
            min = Vector3.one * float.PositiveInfinity;
            max = Vector3.one * float.NegativeInfinity;
            var inverse = Quaternion.Inverse(rotation);
            foreach (var r in renderers)
            {
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var point = b.center + Vector3.Scale(b.extents, new Vector3(
                        (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    point = inverse * point;
                    if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z))
                        throw new InvalidOperationException("모델 Bounds에 유효하지 않은 값이 있습니다.");
                    min = Vector3.Min(min, point); max = Vector3.Max(max, point);
                }
            }
        }

        public static IconCaptureResult Capture(GameObject prefab, IconCaptureSettings settings, IconCaptureEntry entry)
        {
            if (prefab == null) throw new ArgumentException("Prefab을 지정하세요.");
            if (!Finite(entry.scale) || entry.scale <= 0f || !Finite(entry.offset.x) || !Finite(entry.offset.y))
                throw new ArgumentException("배율과 위치 값이 유효하지 않습니다.");
            if (settings.resolution != 128 && settings.resolution != 256
                && settings.resolution != 512 && settings.resolution != 1024)
                throw new ArgumentException("지원 해상도: 128, 256, 512, 1024.");
            PreviewRenderUtility preview = null;
            bool renderShadows = settings.RenderShadows;
            var matteMaterials = settings.removeGloss ? new Dictionary<Material, Material>() : null;
            Texture2D result = null;
            RenderTexture readTarget = null;
            var previousTarget = RenderTexture.active;
            try
            {
                preview = new PreviewRenderUtility();
                var holder = new GameObject("Item Icon Capture") { hideFlags = HideFlags.HideAndDontSave };
                holder.SetActive(false);
                preview.AddSingleGO(holder);
                var model = UnityEngine.Object.Instantiate(prefab, holder.transform);
                model.name = "Icon model";
                foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
                foreach (var light in model.GetComponentsInChildren<Light>(true)) light.enabled = false;
                foreach (var camera in model.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var volume in model.GetComponentsInChildren<Volume>(true)) volume.enabled = false;
                foreach (var particle in model.GetComponentsInChildren<ParticleSystem>(true))
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) r.enabled = false;
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                holder.SetActive(true);
                model.SetActive(true);
                var renderers = Renderers(model);
                foreach (var r in renderers)
                {
                    r.shadowCastingMode = renderShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    r.receiveShadows = renderShadows;
                    r.lightProbeUsage = LightProbeUsage.Off;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    if (r is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                }
                if (matteMaterials != null) ApplyMatteMaterials(renderers, matteMaterials);

                // Normalize only the preview parent: small FBX units and huge world scales remain stable.
                ProjectBounds(renderers, Quaternion.identity, out var initialMin, out var initialMax);
                float extent = (initialMax - initialMin).magnitude;
                if (!Finite(extent) || extent <= 0.0000001f) throw new InvalidOperationException("모델 Bounds 크기가 0입니다.");
                holder.transform.position = -(initialMin + initialMax) * 0.5f;
                var pivot = new GameObject("Icon rotation") { hideFlags = HideFlags.HideAndDontSave };
                preview.AddSingleGO(pivot);
                holder.transform.SetParent(pivot.transform, true);
                pivot.transform.localScale = Vector3.one / extent;
                pivot.transform.rotation = Quaternion.Euler(entry.rotation);
                var direction = Quaternion.Euler(settings.cameraAngles);
                ProjectBounds(renderers, direction, out var min, out var max);
                var size = max - min;
                var center = direction * ((min + max) * 0.5f);
                float fill = Mathf.Clamp(settings.fill, 0.5f, 0.95f);
                float half = entry.autoFit ? Mathf.Max(size.x, size.y) * 0.5f / fill : 0.5f;
                half = Mathf.Max(0.0001f, half / entry.scale);
                var cam = preview.camera;
                cam.orthographic = settings.orthographic; cam.orthographicSize = half; cam.aspect = 1f;
                cam.fieldOfView = 30f;
                float depth = size.z;
                float distance = settings.orthographic ? depth + 2f
                    : half / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) + depth * 0.5f + 0.1f;
                cam.transform.rotation = direction;
                cam.transform.position = center - cam.transform.forward * distance
                    - cam.transform.right * (entry.offset.x * 2f * half)
                    - cam.transform.up * (entry.offset.y * 2f * half);
                cam.nearClipPlane = Mathf.Max(0.001f, distance - depth * 0.5f - 1f);
                cam.farClipPlane = distance + depth * 0.5f + 2f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.clear;
                cam.allowHDR = false; cam.allowMSAA = false;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false; data.renderShadows = renderShadows;
                data.volumeLayerMask = 0; data.antialiasing = AntialiasingMode.None;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
                if (settings.lighting == IconLighting.LitNoShadows)
                {
                    // Neutral fill lights keep the face readable from the capture angle.
                    preview.ambientColor = new Color(0.55f, 0.55f, 0.55f);
                    preview.lights[0].intensity = 0.8f;
                    preview.lights[0].transform.rotation = direction * Quaternion.Euler(25f, -35f, 0f);
                    preview.lights[1].intensity = 0.45f;
                    preview.lights[1].transform.rotation = direction * Quaternion.Euler(-15f, 45f, 0f);
                }
                else
                {
                    bool bright = settings.lighting == IconLighting.Bright;
                    preview.ambientColor = bright
                        ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.25f, 0.25f, 0.25f);
                    preview.lights[0].intensity = bright ? 1.1f : 1.35f;
                    preview.lights[0].transform.rotation = direction * Quaternion.Euler(35f, -35f, 0f);
                    preview.lights[1].intensity = bright ? 0.9f : 0.65f;
                    preview.lights[1].transform.rotation = direction * Quaternion.Euler(-20f, 145f, 0f);
                }
                foreach (var light in preview.lights)
                    light.shadows = renderShadows ? LightShadows.Soft : LightShadows.None;
                int resolution = settings.resolution;
                // Supersampling avoids relying on the game's antialiasing / post-processing.
                preview.BeginPreview(new Rect(0, 0, resolution * 2, resolution * 2), GUIStyle.none);
                RenderPreview(preview, renderShadows);
                var rendered = preview.EndPreview();
                readTarget = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(rendered, readTarget);
                RenderTexture.active = readTarget;
                result = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
                result.hideFlags = HideFlags.HideAndDontSave;
                result.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0); result.Apply();
                var pixels = result.GetPixels32();
                int xMin = resolution, yMin = resolution, xMax = -1, yMax = -1;
                for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++)
                {
                    int index = y * resolution + x; var pixel = pixels[index];
                    if (pixel.a > 8)
                    { xMin = Mathf.Min(xMin, x); xMax = Mathf.Max(xMax, x); yMin = Mathf.Min(yMin, y); yMax = Mathf.Max(yMax, y); }
                    if (settings.blackSilhouette)
                    {
                        pixels[index] = new Color32(0, 0, 0, pixel.a);
                    }
                    else if (pixel.a > 0 && pixel.a < 255)
                    {
                        // Render targets store coverage-premultiplied edges. PNG needs straight alpha.
                        float factor = 255f / pixel.a;
                        pixel.r = (byte)Mathf.Min(255, Mathf.RoundToInt(pixel.r * factor));
                        pixel.g = (byte)Mathf.Min(255, Mathf.RoundToInt(pixel.g * factor));
                        pixel.b = (byte)Mathf.Min(255, Mathf.RoundToInt(pixel.b * factor)); pixels[index] = pixel;
                    }
                }
                if (xMax < 0) throw new InvalidOperationException("촬영 결과에 보이는 픽셀이 없습니다. 머티리얼·방향을 확인하세요.");
                result.SetPixels32(pixels); result.Apply();
                var capture = new IconCaptureResult
                {
                    texture = result, occupancy = Mathf.Max(xMax - xMin + 1, yMax - yMin + 1) / (float)resolution,
                    touchesEdge = xMin <= 1 || yMin <= 1 || xMax >= resolution - 2 || yMax >= resolution - 2
                };
                result = null;
                return capture;
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (readTarget != null) RenderTexture.ReleaseTemporary(readTarget);
                if (result != null) UnityEngine.Object.DestroyImmediate(result);
                if (matteMaterials != null)
                {
                    foreach (var material in matteMaterials.Values)
                        UnityEngine.Object.DestroyImmediate(material);
                }
                preview?.Cleanup();
            }
        }

        static void ApplyMatteMaterials(IEnumerable<Renderer> renderers, Dictionary<Material, Material> copies)
        {
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null) continue;
                    if (!copies.TryGetValue(source, out var matte))
                    {
                        var keywords = source.shader.keywordSpace;
                        var specularOff = keywords.FindKeyword("_SPECULARHIGHLIGHTS_OFF");
                        var reflectionsOff = keywords.FindKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                        var specularColor = keywords.FindKeyword("_SPECULAR_COLOR");
                        var specularMap = keywords.FindKeyword("_SPECGLOSSMAP");
                        if (!specularOff.isValid && !reflectionsOff.isValid
                            && !specularColor.isValid && !specularMap.isValid) continue;

                        matte = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
                        copies.Add(source, matte);
                        if (specularOff.isValid) matte.EnableKeyword(specularOff);
                        if (reflectionsOff.isValid) matte.EnableKeyword(reflectionsOff);
                        // URP Simple Lit enables specular with either a color or a map keyword.
                        if (specularColor.isValid && !specularOff.isValid) matte.DisableKeyword(specularColor);
                        if (specularMap.isValid && !specularOff.isValid) matte.DisableKeyword(specularMap);
                    }
                    materials[i] = matte;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        static void RenderPreview(PreviewRenderUtility preview, bool renderShadows)
        {
            // SSAO is a renderer feature and can darken the model even with camera shadows off.
            var disabledFeatures = new List<ScreenSpaceAmbientOcclusion>();
            try
            {
                if (!renderShadows && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
                {
                    foreach (var rendererData in pipeline.rendererDataList)
                    {
                        if (rendererData == null) continue;
                        foreach (var feature in rendererData.rendererFeatures)
                        {
                            if (feature is ScreenSpaceAmbientOcclusion ssao && ssao != null && ssao.isActive)
                            {
                                disabledFeatures.Add(ssao);
                                ssao.SetActive(false);
                            }
                        }
                    }
                }
                preview.Render(true);
            }
            finally
            {
                foreach (var feature in disabledFeatures) feature.SetActive(true);
            }
        }

        public static string ValidateFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("출력 폴더를 지정하세요.");
            folder = folder.Replace('\\', '/').TrimEnd('/');
            string assets = Path.GetFullPath(Application.dataPath);
            string full = Path.GetFullPath(Path.Combine(Directory.GetParent(assets).FullName, folder));
            if (full != assets && !full.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("출력은 Assets 내부 폴더만 허용합니다.");
            return "Assets" + full.Substring(assets.Length).Replace('\\', '/');
        }

        public static string Save(IconCaptureResult capture, string name, IconCaptureSettings settings)
        {
            string folder = ValidateFolder(settings.outputFolder);
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            name = name.Trim().Trim('.');
            if (string.IsNullOrEmpty(name)) name = "ItemIcon";
            string path = folder + "/" + name + ".png";
            if (File.Exists(path))
            {
                if (settings.overwrite == IconOverwrite.Skip) return null;
                if (settings.overwrite == IconOverwrite.UniqueName) path = AssetDatabase.GenerateUniqueAssetPath(path);
            }
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(path, capture.texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new IOException("PNG 임포터를 찾을 수 없습니다: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            var importSettings = new TextureImporterSettings(); importer.ReadTextureSettings(importSettings);
            importSettings.spriteAlignment = (int)SpriteAlignment.Center;
            importSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(importSettings);
            importer.sRGBTexture = true; importer.mipmapEnabled = false; importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = settings.filter;
            importer.textureCompression = settings.compression; importer.maxTextureSize = settings.resolution;
            importer.SaveAndReimport();
            return path;
        }
    }
}
