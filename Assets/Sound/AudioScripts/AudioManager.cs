using UnityEngine;

/// <summary>
/// Local audio playback for the current game client.
/// Own exactly one instance in a persistent scene object.
/// This class handles local SFX and music; it does not handle network replication.
/// </summary>
[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Local playback sources")]
    [SerializeField] private AudioSource localSfxSource;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource temporaryMusicSource;

    private bool isTemporaryMusicActive;
    private bool shouldResumeMainMusicAfterTemporary;

    public bool IsTemporaryMusicActive => isTemporaryMusicActive;
    public bool IsMainMusicPlaying => musicSource != null && musicSource.isPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ValidateSources();
        ConfigureLocalSources();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Plays a non-spatial sound for this client only (for example, UI feedback).</summary>
    public void PlayLocalSFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || localSfxSource == null)
            return;

        localSfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    /// <summary>
    /// Starts/replaces the main music track. Starting new main music stops any temporary track.
    /// Main and temporary sources should both route to the Music AudioMixer group.
    /// </summary>
    public void PlayMusic(AudioClip clip, bool loop = true, bool interruptTemporary = false)
    {
        if (clip == null || musicSource == null)
            return;
        if (interruptTemporary){
            ClearTemporaryMusic(resumeMainMusic: false);
            musicSource.Stop();
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.time = 0f;
            musicSource.Play();
        }
        else
        {
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.time = 0f;
        }
    }

    /// <summary>
    /// Plays an overlay/temporary track (for example, pause-menu music).
    /// The main track is paused only if it was playing, and can later resume at its old position.
    /// Calling this again replaces the temporary track without losing the original resume state.
    /// </summary>
    public void PlayTemporaryMusic(AudioClip clip, bool loop = true)
    {
        if (clip == null || temporaryMusicSource == null || musicSource == null)
            return;

        if (!isTemporaryMusicActive)
        {
            shouldResumeMainMusicAfterTemporary = musicSource.isPlaying;

            if (shouldResumeMainMusicAfterTemporary)
                musicSource.Pause();
        }

        temporaryMusicSource.Stop();
        temporaryMusicSource.clip = clip;
        temporaryMusicSource.loop = loop;
        temporaryMusicSource.time = 0f;
        temporaryMusicSource.Play();
        isTemporaryMusicActive = true;
    }

    /// <summary>Stops temporary music and resumes the main track if it was playing beforehand.</summary>
    public void StopTemporaryMusic(bool resumeMainMusic = true)
    {
        ClearTemporaryMusic(resumeMainMusic);
    }

    public void PauseMainMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
            musicSource.Pause();

        // An explicit pause means the main track should not be resumed automatically
        // when temporary music ends.
        if (isTemporaryMusicActive)
            shouldResumeMainMusicAfterTemporary = false;
    }

    public void ResumeMainMusic()
    {
        // Avoid overlapping the main track with an active temporary track.
        if (!isTemporaryMusicActive && musicSource != null && musicSource.clip != null)
            musicSource.UnPause();
    }

    public void StopMainMusic()
    {
        if (musicSource != null)
            musicSource.Stop();

        shouldResumeMainMusicAfterTemporary = false;
    }

    public void StopAllMusic()
    {
        ClearTemporaryMusic(resumeMainMusic: false);
        StopMainMusic();
    }

    private void ClearTemporaryMusic(bool resumeMainMusic)
    {
        if (temporaryMusicSource != null)
        {
            temporaryMusicSource.Stop();
            temporaryMusicSource.clip = null;
        }

        bool shouldResume = isTemporaryMusicActive && shouldResumeMainMusicAfterTemporary;
        isTemporaryMusicActive = false;
        shouldResumeMainMusicAfterTemporary = false;

        if (resumeMainMusic && shouldResume && musicSource != null && musicSource.clip != null)
            musicSource.UnPause();
    }

    private void ValidateSources()
    {
        if (localSfxSource == null)
            Debug.LogWarning("AudioManager: Local SFX AudioSource is not assigned.", this);

        if (musicSource == null)
            Debug.LogWarning("AudioManager: Main music AudioSource is not assigned.", this);

        if (temporaryMusicSource == null)
            Debug.LogWarning("AudioManager: Temporary music AudioSource is not assigned.", this);
    }

    private void ConfigureLocalSources()
    {
        if (localSfxSource != null)
        {
            localSfxSource.playOnAwake = false;
            localSfxSource.spatialBlend = 0f;
        }

        if (musicSource != null)
        {
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
        }

        if (temporaryMusicSource != null)
        {
            temporaryMusicSource.playOnAwake = false;
            temporaryMusicSource.spatialBlend = 0f;
        }
    }
}
