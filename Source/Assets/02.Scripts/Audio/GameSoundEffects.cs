using System;
using UnityEngine;
using UnityEngine.Audio;

internal enum GameSoundId
{
    MenuClick,
    Trade,
    Rejected,
    WaterEntry,
    KnifeSwing,
    HarpoonShot,
    GunShot,
    WeaponHit,
    DiverHit,
    OxygenLow,
    OxygenEmpty,
    Pickup,
    DiveComplete,
    Order,
    PlatePickup,
    Served,
    SeatCleaned,
    DrinkStart,
    Settlement,
    OpenUi,
    Dialogue,
    CloseUi,
    Bite = 23,
    UseInteractable,
    SignLights,
    FoodDiscard,
    FinishCook,
    ScreenCrack,
    CleanLoop,
    DrinkDrip
}

internal sealed class GameSoundEffects : ScriptableObject
{
    [Serializable]
    internal struct Cue
    {
        [SerializeField] internal GameSoundId Id;
        [SerializeField] internal AudioClip Clip;
        [SerializeField, Range(0f, 1f)] internal float Volume;
        [SerializeField, Range(0.5f, 2f)] internal float Pitch;
    }

    [Serializable]
    internal struct MusicCue
    {
        [SerializeField] internal AudioClip Intro;
        [SerializeField] internal AudioClip Loop;
        [SerializeField] internal AudioClip AmbienceLoop;
        [SerializeField, Range(0f, 1f)] internal float AmbienceVolume;
    }

    [Header("효과음")]
    [SerializeField] AudioMixerGroup _effectsGroup;
    [SerializeField] Cue[] _cues;

    [Header("배경음악")]
    [SerializeField] AudioMixerGroup _musicGroup;
    [SerializeField] MusicCue _titleMusic;
    [SerializeField] MusicCue _boatMusic;
    [SerializeField] MusicCue _diveMusic;
    [SerializeField] MusicCue _sushiMusic;

    internal AudioMixerGroup EffectsGroup => _effectsGroup;
    internal AudioMixerGroup MusicGroup => _musicGroup;

    internal bool TryGetMusicCue(string sceneName, out MusicCue cue)
    {
        cue = sceneName switch
        {
            "StartScene" => _titleMusic,
            "MorningBoat" or "AfterNoonBoat" or "NightBoat" or
            "TutorialMorningBoat" or "TutorialAfterNoonBoat" or "TutorialNightBoat" => _boatMusic,
            "Dive" or "TutorialDive" => _diveMusic,
            GameManager.NightScene or "TutorialSushi" => _sushiMusic,
            _ => default
        };
        return cue.Loop != null;
    }

    internal bool TryGetCue(GameSoundId id, out Cue cue)
    {
        if (_cues != null)
        {
            foreach (Cue candidate in _cues)
            {
                if (candidate.Id != id || candidate.Clip == null) continue;
                cue = candidate;
                return true;
            }
        }

        cue = default;
        return false;
    }
}
