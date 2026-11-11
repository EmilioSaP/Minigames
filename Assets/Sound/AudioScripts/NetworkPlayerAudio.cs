using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Thin NGO adapter for discrete sounds caused by the owning player.
/// The owner hears the sound immediately; the server relays the event to all other clients.
/// Put this component on the same NetworkObject as the player.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkPlayerAudio : NetworkBehaviour
{
    [SerializeField] private PlayerAudio playerAudio;

    private void Awake()
    {
        if (playerAudio == null)
            playerAudio = GetComponent<PlayerAudio>();

        if (playerAudio == null)
            Debug.LogWarning("NetworkPlayerAudio: PlayerAudio component is not assigned or present.", this);
    }

    /// <summary>
    /// Call on the owning client after the gameplay action has actually been accepted/performed.
    /// Plays locally now, then asks the server to notify all clients except this owner.
    /// </summary>
    public void PlayFromOwner(PlayerSoundEvent soundEvent)
    {
        if (!IsSpawned || !IsOwner || playerAudio == null)
            return;

        // Immediate local feedback. The NotOwner RPC below prevents the owner hearing it twice.
        playerAudio.Play(soundEvent);
        RelaySoundToServerRpc(soundEvent);
    }

    /// <summary>
    /// Call only from server-side gameplay logic for a server-originated event (for example,
    /// a server-controlled AI/object). All clients, including a host client, hear it.
    /// </summary>
    public void PlayForEveryoneFromServer(PlayerSoundEvent soundEvent)
    {
        if (!IsSpawned || !IsServer)
            return;

        PlayForEveryoneClientRpc(soundEvent);
    }

    [Rpc(SendTo.Server)]
    private void RelaySoundToServerRpc(PlayerSoundEvent soundEvent, RpcParams rpcParams = default)
    {
        // Universal RPCs are not ownership-restricted by default, so validate the sender.
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        RelaySoundToOtherClientsRpc(soundEvent);
    }

    [Rpc(SendTo.NotOwner)]
    private void RelaySoundToOtherClientsRpc(PlayerSoundEvent soundEvent)
    {
        if (playerAudio != null)
            playerAudio.Play(soundEvent);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayForEveryoneClientRpc(PlayerSoundEvent soundEvent)
    {
        if (playerAudio != null)
            playerAudio.Play(soundEvent);
    }
}
