
using System;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerSpawnHandler : NetworkBehaviour
{
    public bool HasAppliedSpawn { get; private set; }

    // Server-side notification that the respawn teleport has been applied.
    public event Action RespawnTeleportAppliedServer;
    public event Action<ulong> RespawnRequestedServer;

    private bool respawnTeleportPending;

    public void ShowDeathUi(
        bool canRespawn,
        bool shouldAutoRespawn,
        float delay)
    {
        if (!IsServer || !IsSpawned)
            return;

        ShowDeathUiRpc(canRespawn, shouldAutoRespawn, delay);
    }

    public void UpdateRespawnCountdown(int seconds)
    {
        if (!IsServer || !IsSpawned)
            return;

        UpdateRespawnCountdownRpc(seconds);
    }

    public void BeginRespawnFadeIn()
    {
        if (!IsServer || !IsSpawned)
            return;

        BeginRespawnFadeInRpc();
    }

    public void BeginRespawnFadeOut()
    {
        if (!IsServer || !IsSpawned)
            return;

        BeginRespawnFadeOutRpc();
    }

    [Rpc(SendTo.Owner)]
    private void ShowDeathUiRpc(
        bool canRespawn,
        bool shouldAutoRespawn,
        float delay)
    {
        RespawnUI respawnUI = RespawnUI.FindForScene(gameObject.scene);

        if (respawnUI == null)
        {
            Debug.LogError(
                $"No RespawnUI is available in scene '{gameObject.scene.name}'."
            );
            return;
        }

        respawnUI.ShowDeathState(
            canRespawn,
            shouldAutoRespawn,
            delay,
            this,
            GetComponent<DisablePlayer>()
        );
    }

    [Rpc(SendTo.Owner)]
    private void UpdateRespawnCountdownRpc(int seconds)
    {
        RespawnUI.FindForScene(gameObject.scene)?.UpdateCountdown(seconds);
    }

    [Rpc(SendTo.Owner)]
    private void BeginRespawnFadeInRpc()
    {
        RespawnUI.FindForScene(gameObject.scene)?.BeginRespawnFadeIn(
            GetComponent<DisablePlayer>()
        );
    }

    [Rpc(SendTo.Owner)]
    private void BeginRespawnFadeOutRpc()
    {
        RespawnUI.FindForScene(gameObject.scene)?.BeginRespawnFadeOut();
    }

    [Rpc(SendTo.Server)]
    public void RequestRespawnRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || !IsSpawned ||
            rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            return;
        }

        RespawnRequestedServer?.Invoke(OwnerClientId);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        HasAppliedSpawn = false;
    }

    // --------------------------------------------------
    // INITIAL SPAWNING
    // --------------------------------------------------

    public void ApplySpawnFromServer(
        Vector3 position,
        Quaternion rotation,
        bool restrictMovement)
    {
        if (!IsServer)
            return;

        if (IsOwner)
        {
            // The host owns its local player, so it can move it directly.
            ApplySpawnLocally(position, rotation, restrictMovement);
        }
        else
        {
            // For remote players, ask the owning client to apply
            // the spawn position using owner-authoritative movement.
            // Send the spawn assignment and restriction to the owner.
            ApplySpawnPositionRpc(position, rotation, restrictMovement);
        }
    }

    [Rpc(SendTo.Owner)]
    private void ApplySpawnPositionRpc(
        Vector3 position,
        Quaternion rotation,
        bool restrictMovement)
    {
        ApplySpawnLocally(position, rotation, restrictMovement);
    }

    private void ApplySpawnLocally(
        Vector3 position,
        Quaternion rotation,
        bool restrictMovement)
    {
        if (!IsOwner)
            return;

        HasAppliedSpawn = false;

        ApplyTeleportLocally(position, rotation);

        DisablePlayer disablePlayer = GetComponent<DisablePlayer>();

        if (disablePlayer != null)
            disablePlayer.SpawnRestrict(restrictMovement);

        HasAppliedSpawn = true;
    }

    public void ResetSpawnStatus()
    {
        if (IsOwner)
            HasAppliedSpawn = false;
    }

    // --------------------------------------------------
    // RESPAWNING
    // --------------------------------------------------

    // Server requests a respawn teleport.
    // Returns false if another teleport is already pending.
    public bool RespawnFromServer(
        Vector3 position,
        Quaternion rotation)
    {
        if (!IsServer || !IsSpawned || respawnTeleportPending)
            return false;

        respawnTeleportPending = true;

        if (IsOwner)
        {
            // Host's local player.
            ApplyTeleportLocally(position, rotation);
            CompleteRespawnTeleportServer();
        }
        else
        {
            // Remote player's owner.
            RespawnPositionRpc(position, rotation);
        }

        return true;
    }

    [Rpc(SendTo.Owner)]
    private void RespawnPositionRpc(
        Vector3 position,
        Quaternion rotation)
    {
        if (!IsOwner)
            return;

        ApplyTeleportLocally(position, rotation);

        // Confirm to the server that the teleport was applied.
        AcknowledgeRespawnTeleportRpc();
    }

    [Rpc(SendTo.Server)]
    private void AcknowledgeRespawnTeleportRpc(
        RpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        // Only the actual owner can acknowledge this teleport.
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        CompleteRespawnTeleportServer();
    }

    private void CompleteRespawnTeleportServer()
    {
        if (!IsServer || !respawnTeleportPending)
            return;

        respawnTeleportPending = false;

        RespawnTeleportAppliedServer?.Invoke();
    }

    // --------------------------------------------------
    // SHARED TELEPORT LOGIC
    // --------------------------------------------------

    private void ApplyTeleportLocally(
    Vector3 position,
    Quaternion rotation)
    {
        // Clear walking, dash, knockback, and gravity velocity.
        PlayerMovement playerMovement = GetComponent<PlayerMovement>();

        if (playerMovement != null)
            playerMovement.ResetMotion();

        CharacterController controller =
            GetComponent<CharacterController>();

        bool wasEnabled = controller != null && controller.enabled;

        if (wasEnabled)
            controller.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (wasEnabled)
            controller.enabled = true;

        // Ensure no previous velocity is carried into the new position.
        if (playerMovement != null)
            playerMovement.ResetMotion();
    }

    public void ReleaseSpawnRestriction()
    {
        if (!IsOwner)
            return;

        DisablePlayer disablePlayer = GetComponent<DisablePlayer>();

        if (disablePlayer != null)
            disablePlayer.SpawnRestrict(false);
    }
}
