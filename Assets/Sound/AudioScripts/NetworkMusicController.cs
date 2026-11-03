using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative soundtrack selection for the currently loaded network scene.
/// The server synchronizes only a playlist index; each client plays its local AudioClip
/// through its own persistent AudioManager.
///
/// Place this component on an in-scene NetworkObject in a scene loaded through NGO's
/// NetworkSceneManager. Every build/client must use the same playlist ordering.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkMusicController : NetworkBehaviour
{
    [Header("Scene playlist")]
    [Tooltip("All clients must have the same clips in the same order.")]
    [SerializeField] private AudioClip[] playlist;

    [Header("Playback")]
    [Tooltip("If enabled, selecting a new track starts it from the beginning and loops it.")]
    [SerializeField] private bool loopTracks = true;

    private readonly NetworkVariable<int> currentTrackIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly List<int> validTrackIndices = new List<int>();

    private int lastAppliedTrackIndex = -1;
    private bool playbackPending;

    /// <summary>The synchronized index into Playlist. -1 means no track is selected.</summary>
    public int CurrentTrackIndex => currentTrackIndex.Value;

    /// <summary>The currently selected local AudioClip, or null if none is selected.</summary>
    public AudioClip CurrentTrackClip => GetClipAtIndex(currentTrackIndex.Value);

    /// <summary>Use this to enable/disable a host-only shuffle button.</summary>
    public bool CanHostShuffle => IsSpawned && IsHost && validTrackIndices.Count > 1;

    public override void OnNetworkSpawn()
    {
        BuildValidTrackIndexCache();

        // Subscribe before the server makes its initial selection.
        currentTrackIndex.OnValueChanged += HandleTrackIndexChanged;

        // The current NetworkVariable value is already synchronized for clients
        // before OnNetworkSpawn is invoked, including late joiners.
        ApplyTrack(currentTrackIndex.Value);

        // Only the server chooses the initial soundtrack for this scene.
        if (IsServer && currentTrackIndex.Value < 0)
            SelectNextTrackOnServer();
    }

    public override void OnNetworkDespawn()
    {
        currentTrackIndex.OnValueChanged -= HandleTrackIndexChanged;
        playbackPending = false;
    }

    private void Update()
    {
        // AudioManager is normally persistent and already exists. This retry also
        // covers unusual scene/bootstrap ordering without polling when not needed.
        if (playbackPending && IsSpawned)
            ApplyTrack(currentTrackIndex.Value);
    }

    /// <summary>
    /// Host UI entry point. The host is also the server, so no RPC is needed.
    /// Calls from non-host clients are ignored.
    /// </summary>
    public void ShuffleFromHost()
    {
        if (!IsSpawned)
        {
            Debug.LogWarning("NetworkMusicController: Cannot shuffle before the NetworkObject is spawned.", this);
            return;
        }

        if (!IsHost)
            return;

        SelectNextTrackOnServer();
    }

    private void SelectNextTrackOnServer()
    {
        if (!IsServer)
            return;

        if (validTrackIndices.Count == 0)
        {
            Debug.LogWarning("NetworkMusicController: Playlist has no assigned, valid AudioClips.", this);
            return;
        }

        int currentPosition = validTrackIndices.IndexOf(currentTrackIndex.Value);
        int nextPosition;

        if (validTrackIndices.Count == 1)
        {
            nextPosition = 0;
        }
        else
        {
            // Exclude the current track when possible, preventing immediate repeats.
            int availableCount = currentPosition >= 0
                ? validTrackIndices.Count - 1
                : validTrackIndices.Count;

            nextPosition = Random.Range(0, availableCount);

            if (currentPosition >= 0 && nextPosition >= currentPosition)
                nextPosition++;
        }

        int selectedIndex = validTrackIndices[nextPosition];
        currentTrackIndex.Value = selectedIndex;

        // Ensure the server/host applies its own selection immediately. If the
        // NetworkVariable callback also runs locally, ApplyTrack's guard prevents
        // duplicate playback.
        ApplyTrack(selectedIndex);
    }

    private void HandleTrackIndexChanged(int previousIndex, int newIndex)
    {
        ApplyTrack(newIndex);
    }

    private void ApplyTrack(int index)
    {
        if (index < 0)
            return;

        if (index == lastAppliedTrackIndex)
        {
            playbackPending = false;
            return;
        }

        AudioClip clip = GetClipAtIndex(index);
        if (clip == null)
        {
            Debug.LogError(
                $"NetworkMusicController: Synchronized playlist index {index} is invalid or has no AudioClip. " +
                "Check that every client has the same playlist and ordering.", this);
            lastAppliedTrackIndex = index; // Avoid logging the same configuration error every frame.
            playbackPending = false;
            return;
        }

        if (AudioManager.Instance == null)
        {
            playbackPending = true;
            return;
        }

        // This does not stop temporary music (e.g. pause-menu music).
        AudioManager.Instance.SetMainMusic(clip, loopTracks);

        lastAppliedTrackIndex = index;
        playbackPending = false;

        Debug.Log($"NetworkMusicController: Selected soundtrack '{clip.name}' (index {index}).", this);
    }

    private void BuildValidTrackIndexCache()
    {
        validTrackIndices.Clear();

        if (playlist == null)
        {
            Debug.LogWarning("NetworkMusicController: Playlist array is not assigned.", this);
            return;
        }

        for (int i = 0; i < playlist.Length; i++)
        {
            if (playlist[i] != null)
                validTrackIndices.Add(i);
            else
                Debug.LogWarning($"NetworkMusicController: Playlist element {i} is empty and will be skipped.", this);
        }

        if (validTrackIndices.Count == 0)
            Debug.LogWarning("NetworkMusicController: Playlist contains no valid AudioClips.", this);
    }

    private AudioClip GetClipAtIndex(int index)
    {
        if (playlist == null || index < 0 || index >= playlist.Length)
            return null;

        return playlist[index];
    }
}
