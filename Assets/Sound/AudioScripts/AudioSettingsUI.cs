using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private AudioSettingsManager audioSettomgsManager;

    [Header("Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private void Start()
    {
        if (audioSettomgsManager == null)
        {
            Debug.LogWarning("AudioSettingsUI: AudioSettingsManager is not assigned.");
            return;
        }

        // Initialize sliders using SetValueWithoutNotify to prevent firing 
        // the OnValueChanged events, which would cause redundant PlayerPrefs saves.
        if (masterSlider != null) masterSlider.SetValueWithoutNotify(audioSettomgsManager.MasterVolume);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(audioSettomgsManager.MusicVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(audioSettomgsManager.SfxVolume);
    }
}