using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class AudioGainWindow : EditorWindow
{
    const float MaxGain = 10f;

    sealed class Entry
    {
        internal AudioClip Clip;
        internal float Gain = 1f;
        internal double SafeGain;
        internal Label PeakStatus;
    }

    static readonly Type AudioUtil = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
    const BindingFlags PreviewFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly MethodInfo PlayPreview = AudioUtil?.GetMethod("PlayPreviewClip", PreviewFlags, null,
        new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
    static readonly MethodInfo StopPreview = AudioUtil?.GetMethod("StopAllPreviewClips", PreviewFlags);

    readonly List<Entry> _entries = new();
    ScrollView _list;
    HelpBox _status;
    AudioClip _preview;

    [MenuItem("Tools/Audio Gain")]
    static void OpenWindow()
    {
        var window = GetWindow<AudioGainWindow>("오디오 게인");
        window.minSize = new Vector2(460f, 250f);
    }

    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.Clear();
        root.style.paddingLeft = 10;
        root.style.paddingRight = 10;
        root.style.paddingTop = 8;
        root.style.paddingBottom = 8;

        var picker = new ObjectField("원본 WAV")
        {
            objectType = typeof(AudioClip),
            allowSceneObjects = false
        };
        picker.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue is AudioClip clip) AddClips(new[] { clip });
            picker.SetValueWithoutNotify(null);
        });
        root.Add(picker);

        var toolbar = Row();
        toolbar.Add(new Button(() => AddClips(Selection.GetFiltered<AudioClip>(SelectionMode.Assets))) { text = "Project 선택 추가" });
        toolbar.Add(new Button(StopPlaying) { text = "정지" });
        root.Add(toolbar);

        _list = new ScrollView();
        _list.style.flexGrow = 1;
        root.Add(_list);
        _status = new HelpBox(string.Empty, HelpBoxMessageType.Info);
        _status.style.display = DisplayStyle.None;
        root.Add(_status);
        RefreshList();
    }

    void AddClips(IEnumerable<AudioClip> clips)
    {
        bool added = false;
        string error = null;
        foreach (var clip in clips)
        {
            if (clip == null || _entries.Exists(entry => entry.Clip == clip)) continue;
            try
            {
                var entry = new Entry { Clip = clip };
                ReadSource(entry);
                _entries.Add(entry);
                added = true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }
        }
        if (added) RefreshList();
        if (error != null) SetStatus(error, HelpBoxMessageType.Warning);
        else if (!added) SetStatus("Project에서 추가할 WAV 파일을 선택하세요.", HelpBoxMessageType.Info);
        else ClearStatus();
    }

    void RefreshList()
    {
        _list.Clear();
        if (_entries.Count == 0)
            _list.Add(new Label("PCM 16비트·24비트 WAV를 추가하세요."));
        foreach (var entry in _entries)
        {
            var panel = new VisualElement();
            panel.style.marginTop = 8;
            var name = new Label(entry.Clip != null ? entry.Clip.name : "누락된 파일");
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.tooltip = AssetDatabase.GetAssetPath(entry.Clip);
            panel.Add(name);

            var slider = new Slider("게인 (×)", 0f, MaxGain) { value = entry.Gain, showInputField = true };
            slider.RegisterValueChangedCallback(evt =>
            {
                entry.Gain = Mathf.Clamp(evt.newValue, 0f, MaxGain);
                UpdatePeakStatus(entry);
            });
            panel.Add(slider);
            entry.PeakStatus = new Label();
            UpdatePeakStatus(entry);
            panel.Add(entry.PeakStatus);

            var buttons = Row();
            buttons.Add(new Button(() => TryAction(() => Listen(entry, true))) { text = "원본 듣기" });
            buttons.Add(new Button(() => TryAction(() => Listen(entry, false))) { text = "조정 미리듣기" });
            buttons.Add(new Button(() => TryAction(() => SaveCopy(entry))) { text = "새 WAV로 저장…" });
            buttons.Add(new Button(() =>
            {
                StopPlaying();
                _entries.Remove(entry);
                RefreshList();
            }) { text = "목록 제거" });
            panel.Add(buttons);
            _list.Add(panel);
        }
    }

    static VisualElement Row()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        return row;
    }

    static double AppliedGain(Entry entry) => Math.Min(entry.Gain, entry.SafeGain);

    static void UpdatePeakStatus(Entry entry)
    {
        if (entry.PeakStatus == null) return;
        double gain = AppliedGain(entry);
        entry.PeakStatus.text = gain < entry.Gain
            ? $"적용 {gain:0.###}×  ·  피크 제한"
            : $"적용 {gain:0.###}×  ·  안전한 최대 {entry.SafeGain:0.###}×";
    }

    static string GetSourcePath(AudioClip clip)
    {
        string assetPath = AssetDatabase.GetAssetPath(clip);
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(assetPath), ".wav", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Assets 폴더의 WAV 파일을 선택하세요.");
        return assetPath;
    }

    static WaveFile ReadSource(Entry entry)
    {
        string path = GetSourcePath(entry.Clip);
        string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        var wave = WaveFile.Read(fullPath);
        entry.SafeGain = wave.GetSafeGain(MaxGain);
        UpdatePeakStatus(entry);
        return wave;
    }

    void Listen(Entry entry, bool original)
    {
        if (PlayPreview == null || StopPreview == null)
            throw new NotSupportedException("현재 Unity Editor에서 미리듣기를 사용할 수 없습니다.");
        var wave = ReadSource(entry);
        double gain = original ? 1d : AppliedGain(entry);
        StopPlaying();
        float[] samples = wave.MakePreview(gain);
        var clip = AudioClip.Create(entry.Clip.name + " Preview", wave.FrameCount, wave.Channels, wave.SampleRate, false);
        clip.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            // Unity reinitializes the preview clip buffer when playback starts.
            PlayPreview.Invoke(null, new object[] { clip, 0, false });
            if (!clip.SetData(samples, 0)) throw new IOException("미리듣기 파형을 준비할 수 없습니다.");
            _preview = clip;
        }
        catch
        {
            StopPreview.Invoke(null, null);
            DestroyImmediate(clip);
            throw;
        }
        SetStatus(original ? "원본 미리듣기" : $"{gain:0.###}× 게인 미리듣기", HelpBoxMessageType.Info);
    }

    void SaveCopy(Entry entry)
    {
        string source = GetSourcePath(entry.Clip);
        string sourceDirectory = Path.Combine(Application.dataPath, "..", Path.GetDirectoryName(source));
        string path = EditorUtility.SaveFilePanel("게인이 적용된 WAV 저장", Path.GetFullPath(sourceDirectory),
            Path.GetFileNameWithoutExtension(source) + "_Gain", "wav");
        if (string.IsNullOrEmpty(path)) return;
        WriteCopy(entry, path);
    }

    void WriteCopy(Entry entry, string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("출력 파일 확장자를 .wav로 지정하세요.");
        if (File.Exists(path)) throw new IOException("이미 존재하는 파일입니다. 새 이름을 지정하세요.");
        var wave = ReadSource(entry);
        double gain = AppliedGain(entry);
        wave.SaveNew(path, gain);

        string fullPath = Path.GetFullPath(path).Replace('\\', '/');
        string assets = Application.dataPath.Replace('\\', '/') + "/";
        var comparison = Application.platform == RuntimePlatform.WindowsEditor
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (fullPath.StartsWith(assets, comparison))
        {
            string asset = "Assets/" + fullPath.Substring(assets.Length);
            AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
        }
        SetStatus($"{gain:0.###}×로 저장 · {fullPath}", HelpBoxMessageType.Info);
    }

    void TryAction(Action action)
    {
        try { action(); }
        catch (Exception exception) { SetStatus((exception.InnerException ?? exception).Message, HelpBoxMessageType.Error); }
    }

    void SetStatus(string message, HelpBoxMessageType type)
    {
        _status.text = message;
        _status.messageType = type;
        _status.style.display = DisplayStyle.Flex;
    }

    void ClearStatus() => _status.style.display = DisplayStyle.None;

    void StopPlaying()
    {
        if (_preview == null) return;
        StopPreview?.Invoke(null, null);
        DestroyImmediate(_preview);
        _preview = null;
        if (_status != null) ClearStatus();
    }

    void OnDisable() => StopPlaying();

    sealed class WaveFile
    {
        // Leave 1 dB of headroom for playback and codec overshoot.
        const double PeakCeiling = 0.891250938;
        const int Oversample = 4;
        const int Radius = 12;
        static readonly double[,] Interpolation = CreateInterpolation();

        readonly byte[] _bytes;
        readonly int _dataOffset;
        readonly int _sampleCount;
        readonly int _sampleBytes;
        readonly int _scale;
        internal int Channels { get; }
        internal int SampleRate { get; }
        internal int FrameCount => _sampleCount / Channels;

        WaveFile(byte[] bytes, int dataOffset, int dataLength, int channels, int sampleRate, int bits)
        {
            _bytes = bytes;
            _dataOffset = dataOffset;
            _sampleBytes = bits / 8;
            _sampleCount = dataLength / _sampleBytes;
            _scale = 1 << (bits - 1);
            Channels = channels;
            SampleRate = sampleRate;
        }

        internal static WaveFile Read(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 12 || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" ||
                Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
                throw new InvalidDataException("일반 RIFF WAV 파일만 지원합니다.");
            int channels = 0, sampleRate = 0, bits = 0, dataOffset = 0, dataLength = 0, format = 0;
            for (int offset = 12; offset <= bytes.Length - 8;)
            {
                int size = BitConverter.ToInt32(bytes, offset + 4);
                int payload = offset + 8;
                if (size < 0 || size > bytes.Length - payload)
                    throw new InvalidDataException("WAV 데이터가 손상되었습니다.");
                string id = Encoding.ASCII.GetString(bytes, offset, 4);
                if (id == "fmt " && size >= 16)
                {
                    format = BitConverter.ToUInt16(bytes, payload);
                    channels = BitConverter.ToUInt16(bytes, payload + 2);
                    sampleRate = BitConverter.ToInt32(bytes, payload + 4);
                    bits = BitConverter.ToUInt16(bytes, payload + 14);
                }
                if (id == "data")
                {
                    dataOffset = payload;
                    dataLength = size;
                }
                offset = payload + size + (size & 1);
            }
            if (format != 1 || (bits != 16 && bits != 24))
                throw new InvalidDataException("PCM 16비트·24비트 WAV만 지원합니다.");
            if (channels < 1 || channels > 8 || sampleRate <= 0 || dataLength == 0 ||
                dataLength % (channels * (bits / 8)) != 0)
                throw new InvalidDataException("WAV의 채널, 샘플레이트 또는 데이터 길이가 올바르지 않습니다.");
            return new WaveFile(bytes, dataOffset, dataLength, channels, sampleRate, bits);
        }

        int ReadSample(int index)
        {
            int offset = _dataOffset + index * _sampleBytes;
            int low = _bytes[offset] | (_bytes[offset + 1] << 8);
            if (_sampleBytes == 2) return (short)low;
            int raw = low | (_bytes[offset + 2] << 16);
            return (raw << 8) >> 8;
        }

        int GainSample(int index, double gain) => (int)Math.Round(ReadSample(index) * gain);

        internal float[] MakePreview(double gain)
        {
            var samples = new float[_sampleCount];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = GainSample(i, gain) / (float)_scale;
            return samples;
        }

        internal void SaveNew(string path, double gain)
        {
            byte[] result = (byte[])_bytes.Clone();
            int offset = _dataOffset;
            for (int sample = 0; sample < _sampleCount; sample++)
            {
                int value = GainSample(sample, gain);
                if (value < -_scale || value >= _scale)
                    throw new InvalidOperationException("게인이 WAV의 피크 한도를 넘었습니다.");
                for (int part = 0; part < _sampleBytes; part++)
                    result[offset++] = (byte)((value >> (8 * part)) & 255);
            }
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            string staged = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp~");
            try
            {
                File.WriteAllBytes(staged, result);
                File.Move(staged, path);
            }
            finally
            {
                File.Delete(staged);
            }
        }

        internal double GetSafeGain(double maxGain)
        {
            var channelSamples = new int[FrameCount + Radius * 2];
            double peak = 0d;
            for (int channel = 0; channel < Channels; channel++)
            {
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    int sample = ReadSample(frame * Channels + channel);
                    channelSamples[frame + Radius] = sample;
                    peak = Math.Max(peak, Math.Abs(sample));
                }
                for (int left = 0; left <= FrameCount; left++)
                for (int phase = 0; phase < Oversample - 1; phase++)
                {
                    double reconstructed = 0d;
                    for (int tap = 0; tap < Radius * 2; tap++)
                        reconstructed += channelSamples[left + tap] * Interpolation[phase, tap];
                    peak = Math.Max(peak, Math.Abs(reconstructed));
                }
            }
            return peak == 0d ? maxGain : Math.Min(maxGain, PeakCeiling * _scale / peak);
        }

        static double[,] CreateInterpolation()
        {
            var weights = new double[Oversample - 1, Radius * 2];
            for (int phase = 1; phase < Oversample; phase++)
            for (int tap = 0; tap < Radius * 2; tap++)
            {
                double x = tap - Radius + 1 - (double)phase / Oversample;
                double angle = Math.PI * x;
                double window = .5d + .5d * Math.Cos(angle / Radius);
                weights[phase - 1, tap] = Math.Sin(angle) / angle * window;
            }
            return weights;
        }
    }
}
