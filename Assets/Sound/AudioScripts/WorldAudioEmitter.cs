using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// A reusable 3D sound emitter for players, objects, and environment props.
/// Put it on the GameObject whose position should emit the sound.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class WorldAudioEmitter : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField, Min(0f)] private float minDistance = 1f;
    [SerializeField, Min(0.01f)] private float maxDistance = 18f;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = Mathf.Max(minDistance + 0.01f, maxDistance);

        if (sfxMixerGroup != null)
            audioSource.outputAudioMixerGroup = sfxMixerGroup;
    }

    /// <summary>Plays a one-shot sound at this emitter's world position.</summary>
    public void Play(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void StopAll()
    {
        if (audioSource != null)
            audioSource.Stop();
    }
}
