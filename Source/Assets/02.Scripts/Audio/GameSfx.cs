using System.Collections.Generic;
using UnityEngine;

internal static class GameSfx
{
    const int MaxVoices = 12;
    static readonly AudioSource[] Voices = new AudioSource[MaxVoices];
    static readonly List<(AudioSource Source, GameSoundId Id, int OwnerId)> ProtectedVoices = new();
    static GameSoundEffects _catalog;
    static GameObject _root;
    static int _nextVoice;
    static readonly Dictionary<GameSoundId, int> UiCueFrames = new();
    static AudioSource _dialogueVoice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        _catalog = null;
        _root = null;
        _nextVoice = 0;
        UiCueFrames.Clear();
        _dialogueVoice = null;
        System.Array.Clear(Voices, 0, Voices.Length);
        ProtectedVoices.Clear();
    }

    internal static void Play(GameSoundId id) => Play(id, null);


    internal static void PlayAt(GameSoundId id, Vector3 position) => Play(id, position);

    internal static void PlayWithoutRestart(GameSoundId id, Object owner, Vector3? position = null)
    {
        if (owner == null || !TryGetCue(id, out var cue)) return;

        int ownerId = owner.GetInstanceID();
        int availableIndex = -1;
        for (int i = 0; i < ProtectedVoices.Count; i++)
        {
            var voice = ProtectedVoices[i];
            if (voice.Source == null || !voice.Source.isPlaying)
            {
                if (availableIndex < 0) availableIndex = i;
            }
            else if (voice.Id == id && voice.OwnerId == ownerId)
            {
                return;
            }
        }

        if (availableIndex >= 0)
        {
            var source = ProtectedVoices[availableIndex].Source;
            if (source == null) source = CreateVoice("Protected Voice");
            ProtectedVoices[availableIndex] = (source, id, ownerId);
            PlayVoice(source, cue, position);
        }
        else
        {
            var source = CreateVoice("Protected Voice");
            ProtectedVoices.Add((source, id, ownerId));
            PlayVoice(source, cue, position);
        }
    }

    internal static AudioSource CreateLoop(GameSoundId id, Transform parent)
    {
        if (parent == null || !TryGetCue(id, out var cue)) return null;

        var source = CreateSource(parent, id.ToString());
        source.loop = true;
        source.clip = cue.Clip;
        source.volume = cue.Volume;
        source.pitch = cue.Pitch;
        return source;
    }

    internal static void StopDialogue()
    {
        if (_dialogueVoice != null) _dialogueVoice.Stop();
    }

    static void Play(GameSoundId id, Vector3? position)
    {
        if (!TryGetCue(id, out var cue)) return;

        if (id is GameSoundId.MenuClick or GameSoundId.OpenUi or GameSoundId.CloseUi)
        {
            int frame = Time.frameCount;
            if (UiCueFrames.TryGetValue(id, out int lastFrame) && lastFrame == frame) return;
            UiCueFrames[id] = frame;
        }

        AudioSource source;
        if (id == GameSoundId.Dialogue)
        {
            if (_dialogueVoice == null) _dialogueVoice = CreateVoice("Dialogue");
            source = _dialogueVoice;
            source.Stop();
        }
        else source = GetVoice();
        PlayVoice(source, cue, position);
    }

    static bool TryGetCue(GameSoundId id, out GameSoundEffects.Cue cue)
    {
        cue = default;
        if (!Application.isPlaying) return false;
        if (_catalog == null) _catalog = Resources.Load<GameSoundEffects>("GameSoundEffects");
        return _catalog != null && _catalog.EffectsGroup != null && _catalog.TryGetCue(id, out cue);
    }

    static void PlayVoice(AudioSource source, GameSoundEffects.Cue cue, Vector3? position)
    {
        source.transform.position = position.GetValueOrDefault();
        source.spatialBlend = position.HasValue ? 0.5f : 0f;
        source.clip = cue.Clip;
        source.volume = cue.Volume;
        source.pitch = cue.Pitch;
        source.Play();
    }

    static AudioSource GetVoice()
    {
        for (int offset = 0; offset < Voices.Length; offset++)
        {
            int index = (_nextVoice + offset) % Voices.Length;
            AudioSource voice = Voices[index];
            if (voice != null && voice.isPlaying) continue;

            _nextVoice = (index + 1) % Voices.Length;
            if (voice != null) return voice;

            voice = CreateVoice("Voice");
            Voices[index] = voice;
            return voice;
        }

        AudioSource oldest = Voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % Voices.Length;
        oldest.Stop();
        return oldest;
    }

    static AudioSource CreateVoice(string name)
    {
        if (_root == null)
        {
            _root = new GameObject("Game SFX");
            Object.DontDestroyOnLoad(_root);
        }

        return CreateSource(_root.transform, name);
    }

    static AudioSource CreateSource(Transform parent, string name)
    {
        var voiceObject = new GameObject(name);
        voiceObject.transform.SetParent(parent, false);
        var voice = voiceObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.dopplerLevel = 0f;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.minDistance = 4f;
        voice.maxDistance = 25f;
        voice.outputAudioMixerGroup = _catalog.EffectsGroup;
        return voice;
    }
}
