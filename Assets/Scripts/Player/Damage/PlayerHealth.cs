using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    public NetworkVariable<float> CurrentHealth =
        new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<bool> IsDead =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Server-side event. RespawnManager will subscribe to this later.
    public event Action<DamageInfo> ServerDied;

    public bool IsAlive => !IsDead.Value && CurrentHealth.Value > 0f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;
            IsDead.Value = false;
        }
    }

    public void ApplyDamage(DamageInfo damageInfo)
    {
        // Only the server can modify health.
        if (!IsServer || !IsSpawned || !IsAlive)
            return;

        if (damageInfo.Amount <= 0f)
            return;

        CurrentHealth.Value = Mathf.Max(
            0f,
            CurrentHealth.Value - damageInfo.Amount
        );

        Debug.Log(
            $"Player {OwnerClientId} took {damageInfo.Amount} damage. " +
            $"Health: {CurrentHealth.Value}/{maxHealth}"
        );

        if (CurrentHealth.Value <= 0f)
        {
            Die(damageInfo);
        }
    }

    private void Die(DamageInfo damageInfo)
    {
        if (!IsServer || IsDead.Value)
            return;

        IsDead.Value = true;

        Debug.Log($"Player {OwnerClientId} died.");

        ServerDied?.Invoke(damageInfo);
    }

    // Called by the future RespawnManager after a successful respawn.
    public void RestoreHealthServer()
    {
        if (!IsServer)
            return;

        CurrentHealth.Value = maxHealth;
        IsDead.Value = false;
    }

    public void KillServer(ulong sourceClientId = ulong.MaxValue)
    {
        if (!IsServer || !IsAlive)
            return;

        ApplyDamage(
            new DamageInfo(CurrentHealth.Value, sourceClientId)
        );
    }
}