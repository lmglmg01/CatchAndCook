using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class GameBackgroundMusic : MonoBehaviour
{
    const double ScheduleLeadTime = 0.1;

    GameSoundEffects _catalog;
    AudioSource _introSource;
    AudioSource _loopSource;
    AudioSource _ambienceSource;
    Coroutine _playback;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var catalog = Resources.Load<GameSoundEffects>("GameSoundEffects");
        if (catalog == null || catalog.MusicGroup == null) return;

        var root = new GameObject("Game Background Music");
        DontDestroyOnLoad(root);
        var music = root.AddComponent<GameBackgroundMusic>();
        music.Initialize(catalog);
    }

    void Initialize(GameSoundEffects catalog)
    {
        _catalog = catalog;
        _introSource = CreateSource(false);
        _loopSource = CreateSource(true);
        _ambienceSource = CreateSource(true);
    }

    void Start()
    {
        // 설정 UI가 비활성화되어 있어도 저장된 볼륨을 적용한다.
        var musicVolume = PlayerPrefs.GetFloat("CTeam.Settings.v1.Music", 1f);
        var effectsVolume = PlayerPrefs.GetFloat("CTeam.Settings.v1.Effects", 1f);
        _catalog.MusicGroup.audioMixer.SetFloat("MusicVolume", SettingPreferencesView.ToDecibels(musicVolume));
        if (_catalog.EffectsGroup != null)
            _catalog.EffectsGroup.audioMixer.SetFloat("EffectsVolume", SettingPreferencesView.ToDecibels(effectsVolume));

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        PlaySceneMusic(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    AudioSource CreateSource(bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = _catalog.MusicGroup;
        return source;
    }

    void OnActiveSceneChanged(Scene previous, Scene next)
    {
        PlaySceneMusic(next.name);
    }

    void PlaySceneMusic(string sceneName)
    {
        if (_playback != null) StopCoroutine(_playback);
        _playback = null;

        bool hasMusic = _catalog.TryGetMusicCue(sceneName, out var cue);
        StopSource(_introSource, cue);
        StopSource(_loopSource, cue);
        StopSource(_ambienceSource, cue);
        if (hasMusic) _playback = StartCoroutine(ScheduleMusic(cue));
    }

    static void StopSource(AudioSource source, GameSoundEffects.MusicCue next)
    {
        AudioClip stoppedClip = source.clip;
        source.Stop();
        source.clip = null;

        if (stoppedClip != null && stoppedClip != next.Intro && stoppedClip != next.Loop && stoppedClip != next.AmbienceLoop)
            stoppedClip.UnloadAudioData();
    }

    IEnumerator ScheduleMusic(GameSoundEffects.MusicCue cue)
    {
        // 인트로와 환경음은 같은 DSP 시점에 시작하고 음악 루프는 인트로 뒤에 이어진다.
        _introSource.clip = cue.Intro;
        _loopSource.clip = cue.Loop;
        _ambienceSource.clip = cue.AmbienceLoop;
        _ambienceSource.volume = cue.AmbienceVolume;
        LoadClip(cue.Intro);
        LoadClip(cue.Loop);
        LoadClip(cue.AmbienceLoop);
        while (IsLoading(cue.Intro) || IsLoading(cue.Loop) || IsLoading(cue.AmbienceLoop))
            yield return null;

        double startTime = AudioSettings.dspTime + ScheduleLeadTime;
        if (cue.AmbienceLoop != null && cue.AmbienceLoop.loadState == AudioDataLoadState.Loaded)
            _ambienceSource.PlayScheduled(startTime);
        if (cue.Intro != null && cue.Intro.loadState == AudioDataLoadState.Loaded)
        {
            _introSource.PlayScheduled(startTime);
            startTime += (double)cue.Intro.samples / cue.Intro.frequency;
        }

        if (cue.Loop.loadState == AudioDataLoadState.Loaded)
            _loopSource.PlayScheduled(startTime);
    }

    static bool IsLoading(AudioClip clip) => clip != null && clip.loadState == AudioDataLoadState.Loading;

    static void LoadClip(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();
    }
}
