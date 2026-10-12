using System;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(
    typeof(NetworkObject),
    typeof(GrabbableObject),
    typeof(Rigidbody)
)]
public class NetworkGrabbableInteraction :
    NetworkBehaviour, IGrabbableInteraction
{
    private const ulong NoHolder = ulong.MaxValue;
    private const float MaxPickupDistance = 5f;

    public event Action<bool> PickupRequestResolved;

    private NetworkVariable<ulong> holderClientId =
        new(
            NoHolder,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public bool IsHeld => holderClientId.Value != NoHolder;

    public ulong HolderClientId => holderClientId.Value;

    private GrabbableObject settings;
    private Rigidbody body;

    private Collider[] colliders;
    private bool[] originalColliderStates;

    private Collider holderCollider;
    private Transform grabPosition;
    private Quaternion heldRotationOffset;

    private void Awake()
    {
        settings = GetComponent<GrabbableObject>();
        body = GetComponent<Rigidbody>();

        colliders = GetComponentsInChildren<Collider>(true);
        originalColliderStates = new bool[colliders.Length];

        for (int i = 0; i < colliders.Length; i++)
            originalColliderStates[i] = colliders[i].enabled;
    }

    public override void OnNetworkSpawn()
    {
        holderClientId.OnValueChanged += OnHolderChanged;
        ApplyLocalCollisionState();
    }

    public override void OnNetworkDespawn()
    {
        holderClientId.OnValueChanged -= OnHolderChanged;
    }

    private void FixedUpdate()
    {
        if (!IsServer || !IsHeld)
            return;

        if (grabPosition == null)
            FindGrabPosition();

        if (grabPosition == null)
            return;

        Vector3 targetPosition =
            grabPosition.position +
            grabPosition.TransformVector(settings.HeldOffset);

        body.position = targetPosition;

        if (TryGetHolder(out NetworkClient client))
        {
            float holderYaw =
                client.PlayerObject.transform.eulerAngles.y;

            Quaternion holderRotation =
                Quaternion.Euler(0f, holderYaw, 0f);

            body.rotation =
                holderRotation * heldRotationOffset;
        }
    }

    // ---------------------------------------------------------
    // REQUESTS
    // ---------------------------------------------------------

    public void RequestPickup(PickupController requester)
    {
        if (!IsSpawned)
        {
            PickupRequestResolved?.Invoke(false);
            return;
        }

        RequestPickupServerRpc();
    }

    public void RequestDrop(
        PickupController requester,
        GrabbableObject grabbable)
    {
        if (IsSpawned && grabbable == settings)
            RequestDropServerRpc();
    }

    public void RequestThrow(
        PickupController requester,
        GrabbableObject grabbable,
        Vector3 direction,
        float normalizedCharge)
    {
        if (IsSpawned && grabbable == settings)
        {
            RequestThrowServerRpc(
                direction,
                normalizedCharge
            );
        }
    }

    // ---------------------------------------------------------
    // SERVER - PICKUP
    // ---------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(
        ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        bool accepted = TryPickupOnServer(sender);

        PickupResolvedClientRpc(
            accepted,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { sender }
                }
            }
        );
    }

    private bool TryPickupOnServer(ulong clientId)
    {
        if (!IsServer || IsHeld || settings == null || body == null)
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

        if (pickupController == null ||
            pickupController.GrabPosition == null)
        {
            return false;
        }

        Transform requestedGrabPosition =
            pickupController.GrabPosition;

        if (Vector3.Distance(
                requestedGrabPosition.position,
                transform.position) > MaxPickupDistance)
        {
            return false;
        }

        grabPosition = requestedGrabPosition;
        holderCollider =
            client.PlayerObject.GetComponent<Collider>();

        heldRotationOffset =
            Quaternion.Inverse(
                Quaternion.Euler(
                    0f,
                    client.PlayerObject.transform.eulerAngles.y,
                    0f
                )
            ) * transform.rotation;

        ConfigureServerHeldPhysics();

        holderClientId.Value = clientId;

        return true;
    }

    [ClientRpc]
    private void PickupResolvedClientRpc(
        bool accepted,
        ClientRpcParams clientRpcParams = default)
    {
        PickupRequestResolved?.Invoke(accepted);
    }

    // ---------------------------------------------------------
    // SERVER - DROP
    // ---------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    private void RequestDropServerRpc(
        ServerRpcParams rpcParams = default)
    {
        if (!IsHeld ||
            holderClientId.Value != rpcParams.Receive.SenderClientId)
        {
            return;
        }

        ReleaseOnServer(false, Vector3.zero, 0f);
    }

    // ---------------------------------------------------------
    // SERVER - THROW
    // ---------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    private void RequestThrowServerRpc(
        Vector3 direction,
        float normalizedCharge,
        ServerRpcParams rpcParams = default)
    {
        if (!IsHeld ||
            holderClientId.Value != rpcParams.Receive.SenderClientId)
        {
            return;
        }

        ReleaseOnServer(
            true,
            direction,
            Mathf.Clamp01(normalizedCharge)
        );
    }

    private void ReleaseOnServer(
        bool throwObject,
        Vector3 direction,
        float normalizedCharge)
    {
        RestoreServerPhysics();

        holderClientId.Value = NoHolder;

        holderCollider = null;
        grabPosition = null;
        heldRotationOffset = Quaternion.identity;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        if (throwObject && direction.sqrMagnitude > 0.001f)
        {
            float force =
                normalizedCharge * settings.MaxThrowForce;

            body.AddForce(
                direction.normalized * force,
                ForceMode.Impulse
            );
        }
    }

    // ---------------------------------------------------------
    // SERVER - FOLLOW
    // ---------------------------------------------------------

    private void FindGrabPosition()
    {
        if (!TryGetHolder(out NetworkClient client))
            return;

        PickupController pickupController =
            client.PlayerObject.GetComponent<PickupController>();

        if (pickupController != null)
            grabPosition = pickupController.GrabPosition;
    }

    private bool TryGetHolder(out NetworkClient client)
    {
        client = null;

        return IsHeld &&
               NetworkManager.Singleton.ConnectedClients.TryGetValue(
                   holderClientId.Value,
                   out client
               ) &&
               client.PlayerObject != null;
    }

    // ---------------------------------------------------------
    // PHYSICS
    // ---------------------------------------------------------

    private void ConfigureServerHeldPhysics()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;

        if (!settings.WorldCollisionWhileHeld)
        {
            SetCollidersToHeldState();
        }
        else if (holderCollider != null)
        {
            foreach (Collider collider in colliders)
            {
                if (collider != null)
                {
                    Physics.IgnoreCollision(
                        holderCollider,
                        collider,
                        true
                    );
                }
            }
        }

        ApplyLocalCollisionState();
    }

    private void RestoreServerPhysics()
    {
        if (!settings.WorldCollisionWhileHeld)
        {
            RestoreOriginalColliderStates();
        }
        else if (holderCollider != null)
        {
            foreach (Collider collider in colliders)
            {
                if (collider != null)
                {
                    Physics.IgnoreCollision(
                        holderCollider,
                        collider,
                        false
                    );
                }
            }
        }

        body.isKinematic = false;
    }

    // ---------------------------------------------------------
    // LOCAL COLLISION SYNCHRONIZATION
    // ---------------------------------------------------------

    private void OnHolderChanged(ulong previous, ulong current)
    {
        ApplyLocalCollisionState();
    }

    private void ApplyLocalCollisionState()
    {
        if (!IsSpawned || settings == null)
            return;

        if (!settings.WorldCollisionWhileHeld)
        {
            if (IsHeld)
                SetCollidersToHeldState();
            else
                RestoreOriginalColliderStates();

            return;
        }

        NetworkClient localClient =
            NetworkManager.Singleton.LocalClient;

        if (localClient?.PlayerObject == null)
            return;

        Collider localPlayerCollider =
            localClient.PlayerObject.GetComponent<Collider>();

        if (localPlayerCollider == null)
            return;

        bool localPlayerIsHolder =
            IsHeld &&
            holderClientId.Value ==
            NetworkManager.Singleton.LocalClientId;

        foreach (Collider collider in colliders)
        {
            if (collider == null)
                continue;

            Physics.IgnoreCollision(
                localPlayerCollider,
                collider,
                localPlayerIsHolder
            );
        }
    }

    private void SetCollidersToHeldState()
    {
        foreach (Collider collider in colliders)
        {
            if (collider != null)
                collider.enabled = false;
        }
    }

    private void RestoreOriginalColliderStates()
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = originalColliderStates[i];
        }
    }
}