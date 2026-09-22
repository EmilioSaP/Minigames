using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PickupController : NetworkBehaviour
{
    private enum PickupState
    {
        None,
        PickingUp,
        Held,
        ChargingThrow
    }

    [Header("References")]
    [SerializeField] private InteractionDetector interactionDetector;
    [SerializeField] private Transform grabPosition;
    [SerializeField] private GameObject playerView;


    [Header("Throw")]
    [SerializeField] private float minimumThrowCharge = 0.1f;

    private const float MaximumDownwardAngle = 60f;

    private InteractionUI interactionUI;

    private GrabbableObject heldObject;

    private PickupState pickupState = PickupState.None;

    private GrabbableObject pickupTarget;
    private float pickupTimer;

    private float throwChargeTimer;

    public bool IsHoldingObject =>
        heldObject != null;

    public bool IsPickingUp =>
        pickupState == PickupState.PickingUp;

    public bool IsChargingThrow =>
        pickupState == PickupState.ChargingThrow;
        
    public Transform GrabPosition => grabPosition;

    private bool interactionEnabled = true;
    
    public float ThrowChargeNormalized
    {
        get
        {
            if (heldObject == null)
                return 0f;

            if (heldObject.ThrowChargeTime <= 0f)
                return 1f;

            return Mathf.Clamp01(
                throwChargeTimer /
                heldObject.ThrowChargeTime
            );
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        interactionUI = InteractionUI.Instance;

        if (interactionUI == null)
        {
            Debug.LogWarning(
                "PickupController could not find InteractionUI.Instance."
            );
        }
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        UpdateGrabPosition();
        UpdateInteractionUI();

        if (Mouse.current == null)
            return;

        if (!interactionEnabled)
            return;

        switch (pickupState)
        {
            case PickupState.None:
                HandleIdleInput();
                break;

            case PickupState.PickingUp:
                HandlePickupProgress();
                break;

            case PickupState.Held:
                HandleHeldInput();
                break;

            case PickupState.ChargingThrow:
                HandleThrowCharge();
                break;
        }
    }

    // ---------------------------------------------------------
    // IDLE
    // ---------------------------------------------------------

    private void HandleIdleInput()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        GrabbableObject target =
            GetCurrentGrabbable();

        if (target == null)
            return;

        StartPickup(target);
    }

    private void StartPickup(
        GrabbableObject target)
    {
        pickupTarget = target;
        pickupTimer = 0f;

        if (target.PickupTime <= 0f)
        {
            RequestPickup(target);
            return;
        }

        pickupState =
            PickupState.PickingUp;
    }

    // ---------------------------------------------------------
    // PICKUP
    // ---------------------------------------------------------

    private void HandlePickupProgress()
    {
        if (!Mouse.current.leftButton.isPressed)
        {
            CancelPickup();
            return;
        }

        GrabbableObject currentTarget =
            GetCurrentGrabbable();

        if (currentTarget != pickupTarget)
        {
            CancelPickup();
            return;
        }

        pickupTimer += Time.deltaTime;

        if (pickupTimer >= pickupTarget.PickupTime)
        {
            RequestPickup(pickupTarget);
        }
    }

    private void CancelPickup()
    {
        pickupTarget = null;
        pickupTimer = 0f;
        pickupState = PickupState.None;
    }

    // ---------------------------------------------------------
    // NETWORKED PICKUP
    // ---------------------------------------------------------

    private void RequestPickup(
        GrabbableObject target)
    {
        if (target == null)
        {
            CancelPickup();
            return;
        }

        NetworkObject networkObject =
            target.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogWarning(
                $"'{target.name}' has no NetworkObject."
            );

            CancelPickup();
            return;
        }

        RequestPickupServerRpc(
            networkObject.NetworkObjectId
        );

        pickupTarget = null;
        pickupTimer = 0f;
        pickupState = PickupState.Held;

        heldObject = target;
    }

    [ServerRpc]
    private void RequestPickupServerRpc(
        ulong networkObjectId,
        ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
                .TryGetValue(
                    networkObjectId,
                    out NetworkObject networkObject))
        {
            return;
        }

        GrabbableObject grabbable =
            networkObject.GetComponent<GrabbableObject>();

        if (grabbable == null)
            return;

        bool success =
            grabbable.TryPickup(
                rpcParams.Receive.SenderClientId
            );

        if (!success)
        {
            NotifyPickupFailedClientRpc(
                ClientRpcParamsFrom(
                    rpcParams.Receive.SenderClientId
                )
            );
        }
    }

    [ClientRpc]
    private void NotifyPickupFailedClientRpc(
        ClientRpcParams clientRpcParams)
    {
        if (!IsOwner)
            return;

        heldObject = null;
        pickupState = PickupState.None;
    }

    // ---------------------------------------------------------
    // HOLDING
    // ---------------------------------------------------------

    private void HandleHeldInput()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        StartThrowCharge();
    }

    private void StartThrowCharge()
    {
        if (heldObject == null)
            return;

        throwChargeTimer = 0f;
        pickupState =
            PickupState.ChargingThrow;
    }

    // ---------------------------------------------------------
    // THROW CHARGE
    // ---------------------------------------------------------

    private void HandleThrowCharge()
    {
        if (heldObject == null)
        {
            pickupState = PickupState.None;
            return;
        }

        if (Mouse.current.leftButton.isPressed)
        {
            if (heldObject.ThrowChargeTime > 0f)
            {
                throwChargeTimer += Time.deltaTime;

                throwChargeTimer =
                    Mathf.Min(
                        throwChargeTimer,
                        heldObject.ThrowChargeTime
                    );
            }

            return;
        }

        ReleaseThrow();
    }

    private void ReleaseThrow()
    {
        if (heldObject == null)
        {
            ClearHeldState();
            return;
        }

        float normalizedCharge =
            GetThrowCharge();

        if (normalizedCharge <
            minimumThrowCharge)
        {
            RequestDrop();
        }
        else
        {
            RequestThrow(normalizedCharge);
        }
    }

    private float GetThrowCharge()
    {
        if (heldObject == null)
            return 0f;

        if (heldObject.ThrowChargeTime <= 0f)
            return 1f;

        return Mathf.Clamp01(
            throwChargeTimer /
            heldObject.ThrowChargeTime
        );
    }

    // ---------------------------------------------------------
    // NETWORKED DROP
    // ---------------------------------------------------------

    private void RequestDrop()
    {
        if (heldObject == null)
            return;

        NetworkObject networkObject =
            heldObject.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            ClearHeldState();
            return;
        }

        RequestDropServerRpc(
            networkObject.NetworkObjectId
        );

        ClearHeldState();
    }

    [ServerRpc]
    private void RequestDropServerRpc(
        ulong networkObjectId,
        ServerRpcParams rpcParams = default)
    {
        if (!TryGetGrabbable(
                networkObjectId,
                out GrabbableObject grabbable))
        {
            return;
        }

        if (!grabbable.IsHeld)
            return;

        if (grabbable.HolderClientId !=
            rpcParams.Receive.SenderClientId)
        {
            return;
        }

        grabbable.ServerDrop();
    }

    // ---------------------------------------------------------
    // NETWORKED THROW
    // ---------------------------------------------------------

    private void RequestThrow(
        float normalizedCharge)
    {
        if (heldObject == null)
            return;

        NetworkObject networkObject =
            heldObject.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            ClearHeldState();
            return;
        }

        Vector3 direction =
            playerView != null
                ? playerView.transform.forward.normalized
                : transform.forward;

        RequestThrowServerRpc(
            networkObject.NetworkObjectId,
            direction,
            normalizedCharge
        );

        ClearHeldState();
    }

    [ServerRpc]
    private void RequestThrowServerRpc(
        ulong networkObjectId,
        Vector3 direction,
        float normalizedCharge,
        ServerRpcParams rpcParams = default)
    {
        if (!TryGetGrabbable(
                networkObjectId,
                out GrabbableObject grabbable))
        {
            return;
        }

        if (!grabbable.IsHeld)
            return;

        if (grabbable.HolderClientId !=
            rpcParams.Receive.SenderClientId)
        {
            return;
        }

        if (direction.sqrMagnitude < 0.01f)
            direction = Vector3.forward;

        grabbable.ServerThrow(
            direction.normalized,
            Mathf.Clamp01(normalizedCharge)
        );
    }

    // ---------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------

    private void UpdateGrabPosition()
    {
        if (grabPosition == null)
            return;

        Vector3 offset =
            grabPosition.position - transform.position;

        float distance = offset.magnitude;

        if (distance <= Mathf.Epsilon)
            return;

        Vector3 horizontalOffset =
            Vector3.ProjectOnPlane(offset, Vector3.up);

        float downwardAngle =
            Mathf.Atan2(
                -offset.y,
                horizontalOffset.magnitude
            ) * Mathf.Rad2Deg;

        if (downwardAngle <= MaximumDownwardAngle)
            return;

        if (horizontalOffset.sqrMagnitude <= Mathf.Epsilon)
            horizontalOffset = transform.forward;

        Vector3 horizontalDirection =
            horizontalOffset.normalized;

        float maximumDownwardAngleRadians =
            MaximumDownwardAngle * Mathf.Deg2Rad;

        grabPosition.position =
            transform.position +
            horizontalDirection *
                (distance * Mathf.Cos(maximumDownwardAngleRadians)) -
            Vector3.up *
                (distance * Mathf.Sin(maximumDownwardAngleRadians));
    }

    private bool TryGetGrabbable(
        ulong networkObjectId,
        out GrabbableObject grabbable)
    {
        grabbable = null;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
                .TryGetValue(
                    networkObjectId,
                    out NetworkObject networkObject))
        {
            return false;
        }

        grabbable =
            networkObject.GetComponent<GrabbableObject>();

        return grabbable != null;
    }

    private GrabbableObject GetCurrentGrabbable()
    {
        if (interactionDetector == null)
            return null;

        Interactable target =
            interactionDetector.CurrentTarget;

        if (target == null)
            return null;

        if (target.Type !=
            Interactable.InteractionType.Grabbable)
        {
            return null;
        }

        GrabbableObject grabbable =
            target.GetComponent<GrabbableObject>();

        if (grabbable == null)
            return null;

        // Someone is already holding it.
        if (grabbable.IsHeld)
            return null;

        return grabbable;
    }

    private void ClearHeldState()
    {
        heldObject = null;
        pickupTarget = null;

        pickupTimer = 0f;
        throwChargeTimer = 0f;

        pickupState =
            PickupState.None;
    }

    private void UpdateInteractionUI()
    {
        if (interactionUI == null)
            return;

        if (pickupState != PickupState.None)
        {
            interactionUI.HidePrompt();
            return;
        }

        GrabbableObject target =
            GetCurrentGrabbable();

        if (target != null)
            interactionUI.ShowPickupPrompt(target);
        else
            interactionUI.HidePrompt();
    }

    private static ClientRpcParams ClientRpcParamsFrom(
        ulong clientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds =
                    new[] { clientId }
            }
        };
    }

    public void SetInteractionEnabled(bool state)
    {
        interactionEnabled = state;
    }
}