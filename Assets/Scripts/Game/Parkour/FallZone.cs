using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FallZone : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float damageAmount = 9999f;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only the server evaluates hazards.
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        NetworkObject playerObject =
            other.GetComponentInParent<NetworkObject>();

        if (playerObject == null || !playerObject.IsPlayerObject)
            return;

        IDamageable damageable =
            playerObject.GetComponent<IDamageable>();

        if (damageable == null)
        {
            Debug.LogWarning(
                $"Player {playerObject.OwnerClientId} has no IDamageable component."
            );
            return;
        }

        damageable.ApplyDamage(
            new DamageInfo(damageAmount, ulong.MaxValue)
        );
    }
}