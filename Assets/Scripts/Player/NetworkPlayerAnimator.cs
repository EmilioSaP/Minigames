using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Thin NGO adapter for synchronizing Animator states.
/// Reads local movement data if owner, syncs via NetworkVariables, and applies to the Animator for all clients.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkPlayerAnimator : NetworkBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;
    
    [SerializeField] private Animator animator;

    private PlayerHealth playerHealth;
    private bool isDead;

    // NetworkVariables synchronized from the Owner to Everyone else
    // WritePermission.Owner is crucial here so the client can update them directly without server RPCs
    private readonly NetworkVariable<float> netSpeed = new NetworkVariable<float>(
        0f, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<bool> netIsAirborne = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<bool> netIsSpectator = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    // Cache animator hashes for performance
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AirborneHash = Animator.StringToHash("IsAirborne");
    private static readonly int SpectatorHash = Animator.StringToHash("IsSpectator");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int LandHash = Animator.StringToHash("Land");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int RespawnHash = Animator.StringToHash("Respawn");

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        playerHealth = GetComponent<PlayerHealth>();

        if (playerMovement == null)
            Debug.LogWarning("NetworkPlayerAnimator: PlayerMovement component is not assigned or present.", this);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isDead = playerHealth != null && playerHealth.IsDead.Value;
    }

    private void Update()
    {
        if (!IsSpawned || animator == null)
            return;

        if (IsOwner && playerMovement != null)
        {
            netIsSpectator.Value = playerMovement.IsSpectator;

            if (!IsPlayerDead)
            {
                float targetSpeed = 0f;
                if (playerMovement.IsSprinting) targetSpeed = 2f;
                else if (playerMovement.IsMoving) targetSpeed = 1f;

                netSpeed.Value = targetSpeed;
                netIsAirborne.Value = playerMovement.IsAirborne;
            }
        }

        animator.SetBool(SpectatorHash, netIsSpectator.Value);

        if (IsPlayerDead)
            return;

        animator.SetFloat(SpeedHash, netSpeed.Value);
        animator.SetBool(AirborneHash, netIsAirborne.Value);
    }

    /// <summary>
    /// Call on the owning client when the jump action actually occurs.
    /// Triggers locally immediately, then asks the server to relay to other clients.
    /// </summary>
    public void TriggerJumpFromOwner()
    {
        if (!IsSpawned || !IsOwner || animator == null || IsPlayerDead)
            return;

        animator.SetTrigger(JumpHash);
        RelayJumpToServerRpc();
    }

    public void TriggerLandingFromOwner()
    {
        if (!IsSpawned || !IsOwner || animator == null || IsPlayerDead)
            return;

        animator.SetTrigger(LandHash);
        RelayLandingToServerRpc();
    }

    public void ApplyLifeState(bool dead, bool isRespawning)
    {
        isDead = dead;

        if (animator == null)
            return;

        if (dead)
        {
            animator.SetFloat(SpeedHash, 0f);
            animator.SetBool(AirborneHash, false);
            animator.ResetTrigger(JumpHash);
            animator.ResetTrigger(LandHash);
            animator.ResetTrigger(RespawnHash);
            animator.SetTrigger(DeathHash);
        }
        else if (isRespawning)
        {
            animator.ResetTrigger(DeathHash);
            animator.SetTrigger(RespawnHash);
        }
    }

    [Rpc(SendTo.Server)]
    private void RelayJumpToServerRpc(RpcParams rpcParams = default)
    {
        // Validate sender matches the owner
        if (rpcParams.Receive.SenderClientId != OwnerClientId || IsPlayerDead)
            return;

        RelayJumpToOtherClientsRpc();
    }

    [Rpc(SendTo.NotOwner)]
    private void RelayJumpToOtherClientsRpc()
    {
        if (animator != null && !IsPlayerDead)
            animator.SetTrigger(JumpHash);
    }

    [Rpc(SendTo.Server)]
    private void RelayLandingToServerRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || IsPlayerDead)
            return;

        RelayLandingToOtherClientsRpc();
    }

    [Rpc(SendTo.NotOwner)]
    private void RelayLandingToOtherClientsRpc()
    {
        if (animator != null && !IsPlayerDead)
            animator.SetTrigger(LandHash);
    }

    private bool IsPlayerDead =>
        isDead || (playerHealth != null && playerHealth.IsDead.Value);
}