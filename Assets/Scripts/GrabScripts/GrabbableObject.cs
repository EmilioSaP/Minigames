using Unity.Netcode;
using UnityEngine;

public class GrabbableObject : NetworkBehaviour
{
    [Header("Pickup")]
    [SerializeField] private float pickupTime = 0f;
    [SerializeField] private Vector3 heldOffset;

    [Header("Held Physics")]
    [SerializeField] private bool worldCollisionWhileHeld = true;

    [Header("Throw")]
    [SerializeField] private float throwChargeTime = 1f;
    [SerializeField] private float maxThrowForce = 20f;

    [Header("Movement Effects")]
    [SerializeField] private float slowPlayer = 1f;
    [SerializeField] private float slowJump = 1f;

    private const ulong NoHolder = ulong.MaxValue;

    private NetworkVariable<ulong> holderClientId =
        new(
            NoHolder,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private Rigidbody objectRigidbody;
    private Collider[] objectColliders;

    private Collider holderCollider;
    private Transform grabPosition;

    public float PickupTime => pickupTime;
    public Vector3 HeldOffset => heldOffset;

    public bool WorldCollisionWhileHeld =>
        worldCollisionWhileHeld;

    public float ThrowChargeTime => throwChargeTime;
    public float MaxThrowForce => maxThrowForce;

    public float SlowPlayer => slowPlayer;
    public float SlowJump => slowJump;

    public bool IsHeld =>
        holderClientId.Value != NoHolder;

    public ulong HolderClientId =>
        holderClientId.Value;

    private void Awake()
    {
        objectRigidbody = GetComponent<Rigidbody>();

        objectColliders =
            GetComponentsInChildren<Collider>();
    }

    public override void OnNetworkSpawn()
    {
        holderClientId.OnValueChanged += OnHolderChanged;

        if (IsServer)
        {
            if (objectRigidbody == null)
            {
                Debug.LogError(
                    $"'{name}' has no Rigidbody."
                );
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        holderClientId.OnValueChanged -= OnHolderChanged;
    }

    private void FixedUpdate()
    {
        if (!IsServer)
            return;

        if (!IsHeld)
            return;

        UpdateHeldObject();
    }

    // ---------------------------------------------------------
    // SERVER - PICKUP
    // ---------------------------------------------------------

    public bool TryPickup(ulong clientId)
    {
        if (!IsServer)
            return false;

        if (IsHeld)
            return false;

        if (!NetworkManager.Singleton.ConnectedClients
                .TryGetValue(clientId, out NetworkClient client))
        {
            return false;
        }

        if (client.PlayerObject == null)
            return false;

        PickupController pickupController =
            client.PlayerObject.GetComponent<PickupController>();

        if (pickupController == null)
        {
            Debug.LogWarning(
                $"Player {clientId} has no PickupController."
            );

            return false;
        }

        grabPosition = pickupController.GrabPosition;

        if (grabPosition == null)
        {
            Debug.LogWarning(
                $"Player {clientId} has no GrabPosition."
            );

            return false;
        }

        // Validate against the actual hand/hold position.
        float distance =
            Vector3.Distance(
                grabPosition.position,
                transform.position
            );

        if (distance > 5f)
            return false;

        holderClientId.Value = clientId;

        holderCollider =
            client.PlayerObject.GetComponent<Collider>();

        ConfigureHeldPhysics();

        return true;
    }

    // ---------------------------------------------------------
    // SERVER - FOLLOW
    // ---------------------------------------------------------

    private void UpdateHeldObject()
    {
        if (grabPosition == null)
            {
                FindGrabPosition();

                if (grabPosition == null)
                    return;
            }

        Vector3 targetPosition =
            grabPosition.position +
            grabPosition.TransformVector(HeldOffset);

        objectRigidbody.position = targetPosition;
    }

    private void FindGrabPosition()
    {
        if (holderClientId.Value == NoHolder)
            return;

        if (!NetworkManager.Singleton.ConnectedClients
                .TryGetValue(
                    holderClientId.Value,
                    out NetworkClient client))
        {
            return;
        }

        if (client.PlayerObject == null)
            return;

        PickupController pickupController =
            client.PlayerObject.GetComponent<PickupController>();

        if (pickupController != null)
            grabPosition =
                pickupController.GrabPosition;
    }

    // ---------------------------------------------------------
    // SERVER - DROP
    // ---------------------------------------------------------

    public void ServerDrop()
    {
        if (!IsServer)
            return;

        RestorePhysics();

        objectRigidbody.linearVelocity = Vector3.zero;
        objectRigidbody.angularVelocity = Vector3.zero;

        holderClientId.Value = NoHolder;

        holderCollider = null;
        grabPosition = null;
    }

    // ---------------------------------------------------------
    // SERVER - THROW
    // ---------------------------------------------------------

    public void ServerThrow(
        Vector3 direction,
        float normalizedCharge)
    {
        if (!IsServer)
            return;

        if (!IsHeld)
            return;

        direction.Normalize();

        float clampedCharge =
            Mathf.Clamp01(normalizedCharge);

        float throwForce =
            clampedCharge * MaxThrowForce;

        RestorePhysics();

        holderClientId.Value = NoHolder;

        holderCollider = null;
        grabPosition = null;

        objectRigidbody.linearVelocity = Vector3.zero;
        objectRigidbody.angularVelocity = Vector3.zero;

        objectRigidbody.AddForce(
            direction * throwForce,
            ForceMode.Impulse
        );
    }

    // ---------------------------------------------------------
    // PHYSICS
    // ---------------------------------------------------------

    private void ConfigureHeldPhysics()
    {
        objectRigidbody.linearVelocity = Vector3.zero;
        objectRigidbody.angularVelocity = Vector3.zero;

        objectRigidbody.isKinematic = true;

        if (!worldCollisionWhileHeld)
        {
            foreach (Collider collider in objectColliders)
            {
                if (collider != null)
                    collider.enabled = false;
            }

            return;
        }

        if (holderCollider == null)
            return;

        foreach (Collider collider in objectColliders)
        {
            if (collider == null)
                continue;

            Physics.IgnoreCollision(
                holderCollider,
                collider,
                true
            );
        }
    }

    private void RestorePhysics()
    {
        if (!worldCollisionWhileHeld)
        {
            foreach (Collider collider in objectColliders)
            {
                if (collider != null)
                    collider.enabled = true;
            }
        }
        else if (holderCollider != null)
        {
            foreach (Collider collider in objectColliders)
            {
                if (collider == null)
                    continue;

                Physics.IgnoreCollision(
                    holderCollider,
                    collider,
                    false
                );
            }
        }

        objectRigidbody.isKinematic = false;
    }

    private void OnHolderChanged(
        ulong previousHolder,
        ulong newHolder)
    {
    }
}