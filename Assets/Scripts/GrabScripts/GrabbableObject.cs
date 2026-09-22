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

    // Server-side reference to the holder.
    private Collider holderCollider;
    private Quaternion heldRotationOffset;
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

        // Apply the current state in case this object
        // was already held when this client spawned.
        ApplyLocalCollisionState();

        if (IsServer && objectRigidbody == null)
        {
            Debug.LogError(
                $"'{name}' has no Rigidbody."
            );
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

        heldRotationOffset =
            Quaternion.Inverse(
                Quaternion.Euler(0f, client.PlayerObject.transform.eulerAngles.y, 0f)
            ) * transform.rotation;

        if (grabPosition == null)
        {
            Debug.LogWarning(
                $"Player {clientId} has no GrabPosition."
            );

            return false;
        }

        float distance =
            Vector3.Distance(
                grabPosition.position,
                transform.position
            );

        if (distance > 5f)
            return false;

        // Set this before changing the NetworkVariable so the
        // server's OnHolderChanged callback has the reference.
        holderCollider =
            client.PlayerObject.GetComponent<Collider>();

        holderClientId.Value = clientId;

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

    // Position
    Vector3 targetPosition =
        grabPosition.position +
        grabPosition.TransformVector(HeldOffset);

    objectRigidbody.position = targetPosition;

    // Rotation
    if (holderClientId.Value != NoHolder &&
        NetworkManager.Singleton.ConnectedClients
            .TryGetValue(
                holderClientId.Value,
                out NetworkClient holderClient))
    {
        if (holderClient.PlayerObject != null)
        {
            float holderYaw =
                holderClient.PlayerObject
                    .transform.eulerAngles.y;

            Quaternion holderRotation =
                Quaternion.Euler(
                    0f,
                    holderYaw,
                    0f
                );

            Quaternion targetRotation =
                holderRotation *
                heldRotationOffset;

            objectRigidbody.rotation =
                targetRotation;
        }
    }
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
        {
            grabPosition =
                pickupController.GrabPosition;
        }
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
        heldRotationOffset = Quaternion.identity;
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
        heldRotationOffset = Quaternion.identity;

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

        // Collision configuration is also applied through
        // OnHolderChanged so every client gets the proper
        // local collision state.
        ApplyLocalCollisionState();
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

    // ---------------------------------------------------------
    // LOCAL COLLISION STATE
    // ---------------------------------------------------------

    private void ApplyLocalCollisionState()
    {
        if (NetworkManager.Singleton == null)
            return;

        // -----------------------------------------------------
        // Objects that should have NO world collision while held
        // -----------------------------------------------------

        if (!worldCollisionWhileHeld)
        {
            bool shouldDisableColliders = IsHeld;

            foreach (Collider collider in objectColliders)
            {
                if (collider == null)
                    continue;

                collider.enabled = !shouldDisableColliders;
            }

            return;
        }

        // -----------------------------------------------------
        // Objects that keep world collision
        // -----------------------------------------------------

        NetworkClient localClient =
            NetworkManager.Singleton.LocalClient;

        if (localClient == null ||
            localClient.PlayerObject == null)
        {
            return;
        }

        Collider localPlayerCollider =
            localClient.PlayerObject.GetComponent<Collider>();

        if (localPlayerCollider == null)
            return;

        bool isLocalHolder =
            IsHeld &&
            holderClientId.Value ==
            NetworkManager.Singleton.LocalClientId;

        foreach (Collider collider in objectColliders)
        {
            if (collider == null)
                continue;

            Physics.IgnoreCollision(
                localPlayerCollider,
                collider,
                isLocalHolder
            );
        }
    }

    // ---------------------------------------------------------
    // NETWORK STATE CHANGE
    // ---------------------------------------------------------

    private void OnHolderChanged(
        ulong previousHolder,
        ulong newHolder)
    {
        ApplyLocalCollisionState();
    }
}