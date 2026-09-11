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

        UpdateInteractionUI();

        if (Mouse.current == null)
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

        return target.GetComponent<GrabbableObject>();
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
}