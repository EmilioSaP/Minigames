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
    [SerializeField] private AudioClip[] jumpClips;
    [SerializeField] private AudioClip[] throwClips;
    [SerializeField] private AudioClip[] landClips;
    [SerializeField] private AudioClip pickupClip;
    [SerializeField] private AudioClip dropClip;
    [SerializeField] private AudioClip[] hurtClips;
    [SerializeField] private AudioClip explodeClip;
    [SerializeField] private AudioClip spawnClip;

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
    public void PlayHurt(float volumeScale = 1f) => Play(PlayerSoundEvent.Hurt, volumeScale);
    public void PlayExplode(float volumeScale = 1f) => Play(PlayerSoundEvent.Explode, volumeScale);
    public void PlaySpawn(float volumeScale = 1f) => Play(PlayerSoundEvent.Spawn, volumeScale);
    private AudioClip GetClip(PlayerSoundEvent soundEvent)
    {
        switch (soundEvent)
        {
            case PlayerSoundEvent.Jump: return jumpClips.Length > 0 ? jumpClips[Random.Range(0, jumpClips.Length)] : null;
            case PlayerSoundEvent.Throw: return throwClips.Length > 0 ? throwClips[Random.Range(0, throwClips.Length)] : null;
            case PlayerSoundEvent.Land: return landClips.Length > 0 ? landClips[Random.Range(0, landClips.Length)] : null;
            case PlayerSoundEvent.Pickup: return pickupClip;
            case PlayerSoundEvent.Drop: return dropClip;
            case PlayerSoundEvent.Hurt: return hurtClips.Length > 0 ? hurtClips[Random.Range(0, hurtClips.Length)] : null;
            case PlayerSoundEvent.Explode: return explodeClip;
            case PlayerSoundEvent.Spawn: return spawnClip;
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
    Drop = 4,
    Hurt = 5,
    Explode = 6,
    Spawn = 7,
}
