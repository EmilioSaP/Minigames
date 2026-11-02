using UnityEngine;

/// <summary>
/// Maps player gameplay sound events to clips and plays them through a 3D emitter.
/// This class contains no Netcode code; NetworkPlayerAudio handles replication separately.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAudio : MonoBehaviour
{
    [Header("3D emitter")]
    [SerializeField] private WorldAudioEmitter worldEmitter;

    [Header("Player sound clips")]
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip throwClip;
    [SerializeField] private AudioClip landClip;
    [SerializeField] private AudioClip pickupClip;
    [SerializeField] private AudioClip dropClip;

    private void Awake()
    {
        if (worldEmitter == null)
            worldEmitter = GetComponent<WorldAudioEmitter>();

        if (worldEmitter == null)
            Debug.LogWarning("PlayerAudio: Assign a WorldAudioEmitter to this player.", this);
    }

    public void Play(PlayerSoundEvent soundEvent, float volumeScale = 1f)
    {
        if (worldEmitter == null)
            return;

        AudioClip clip = GetClip(soundEvent);
        if (clip == null)
        {
            Debug.LogWarning($"PlayerAudio: No clip assigned for sound event '{soundEvent}'.", this);
            return;
        }

        worldEmitter.Play(clip, volumeScale);
    }

    public void PlayJump(float volumeScale = 1f) => Play(PlayerSoundEvent.Jump, volumeScale);
    public void PlayThrow(float volumeScale = 1f) => Play(PlayerSoundEvent.Throw, volumeScale);
    public void PlayLand(float volumeScale = 1f) => Play(PlayerSoundEvent.Land, volumeScale);
    public void PlayPickup(float volumeScale = 1f) => Play(PlayerSoundEvent.Pickup, volumeScale);
    public void PlayDrop(float volumeScale = 1f) => Play(PlayerSoundEvent.Drop, volumeScale);

    private AudioClip GetClip(PlayerSoundEvent soundEvent)
    {
        switch (soundEvent)
        {
            case PlayerSoundEvent.Jump: return jumpClip;
            case PlayerSoundEvent.Throw: return throwClip;
            case PlayerSoundEvent.Land: return landClip;
            case PlayerSoundEvent.Pickup: return pickupClip;
            case PlayerSoundEvent.Drop: return dropClip;
            default: return null;
        }
    }
}

/// <summary>
/// Event identifiers are sent over the network instead of sending AudioClip references.
/// Add new discrete player events here as needed.
/// </summary>
public enum PlayerSoundEvent : byte
{
    Jump = 0,
    Throw = 1,
    Land = 2,
    Pickup = 3,
    Drop = 4
}
