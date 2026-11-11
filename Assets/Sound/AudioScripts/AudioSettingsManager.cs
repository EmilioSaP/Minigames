using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Loads, applies, and persists the client's audio volume settings.
/// Keep UI Slider references out of this class: connect Slider.OnValueChanged(float)
/// to the matching public Set...Volume method in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public sealed class AudioSettingsManager : MonoBehaviour
{
    private const string MasterKey = "MasterVolume";
    private const string MusicKey = "MusicVolume";
    private const string SfxKey = "SFXVolume";
    private const string VoiceKey = "VoiceVolume";

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterParameter = "MasterVolume";
    [SerializeField] private string musicParameter = "MusicVolume";
    [SerializeField] private string sfxParameter = "SFXVolume";
    [SerializeField] private string voiceParameter = "VoiceVolume";

    [Header("Default volumes (0 to 1)")]
    [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float defaultVoiceVolume = 1f;

    public float MasterVolume { get; private set; }
    public float MusicVolume { get; private set; }
    public float SfxVolume { get; private set; }
    public float VoiceVolume { get; private set; }

    private void Awake()
    {
        LoadSettings();
    }

    private void Start()
    {
        ApplyAllVolumes();
    }

    public void SetMasterVolume(float value) => SetVolume(value, MasterKey, masterParameter, AudioBus.Master);
    public void SetMusicVolume(float value) => SetVolume(value, MusicKey, musicParameter, AudioBus.Music);
    public void SetSFXVolume(float value) => SetVolume(value, SfxKey, sfxParameter, AudioBus.SFX);
    public void SetVoiceVolume(float value) => SetVolume(value, VoiceKey, voiceParameter, AudioBus.Voice);

    public float GetMasterVolume() => MasterVolume;
    public float GetMusicVolume() => MusicVolume;
    public float GetSFXVolume() => SfxVolume;
    public float GetVoiceVolume() => VoiceVolume;

    private void LoadSettings()
    {
        // Read each key independently so partially populated/older PlayerPrefs are handled safely.
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, defaultMasterVolume));
        MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, defaultMusicVolume));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, defaultSfxVolume));
        VoiceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VoiceKey, defaultVoiceVolume));
    }

    private void SetVolume(float value, string preferenceKey, string mixerParameter, AudioBus bus)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(preferenceKey, value);
        PlayerPrefs.Save();

        switch (bus)
        {
            case AudioBus.Master: MasterVolume = value; break;
            case AudioBus.Music: MusicVolume = value; break;
            case AudioBus.SFX: SfxVolume = value; break;
            case AudioBus.Voice: VoiceVolume = value; break;
        }

        ApplyVolume(mixerParameter, value);
    }

    private void ApplyAllVolumes()
    {
        ApplyVolume(masterParameter, MasterVolume);
        ApplyVolume(musicParameter, MusicVolume);
        ApplyVolume(sfxParameter, SfxVolume);
        ApplyVolume(voiceParameter, VoiceVolume);
    }

    private void ApplyVolume(string exposedParameter, float linearVolume)
    {
        if (audioMixer == null)
        {
            Debug.LogWarning("AudioSettingsManager: AudioMixer is not assigned.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(exposedParameter))
        {
            Debug.LogWarning("AudioSettingsManager: An exposed AudioMixer parameter name is empty.", this);
            return;
        }

        // AudioMixer uses decibels. A true zero is represented by a safely low dB value.
        float decibels = linearVolume <= 0.0001f
            ? -80f
            : Mathf.Log10(linearVolume) * 20f;

        if (!audioMixer.SetFloat(exposedParameter, decibels))
        {
            Debug.LogWarning(
                $"AudioSettingsManager: Could not set exposed AudioMixer parameter '{exposedParameter}'. " +
                "Check that it exists and is exposed.", this);
        }
    }

    private enum AudioBus
    {
        Master,
        Music,
        SFX,
        Voice
    }
}
