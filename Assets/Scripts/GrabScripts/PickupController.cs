using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PickupController : NetworkBehaviour
{
    private enum PickupState
    {
        None,
        PickingUp,
        Held
    }

    [Header("References")]
    [SerializeField] private InteractionDetector interactionDetector;
    [SerializeField] private Transform grabPosition;
    [SerializeField] private GameObject playerView;

    [Header("Held Object")]
    [SerializeField] private float holdFollowSpeed = 15f;

    private InteractionUI interactionUI;

    private GrabbableObject heldObject;
    private Rigidbody heldRigidbody;

    private PickupState pickupState = PickupState.None;

    private GrabbableObject pickupTarget;
    private float pickupTimer;

    private Collider[] heldColliders;
    private Collider playerCollider;

    public bool IsHoldingObject => heldObject != null;
    public bool IsPickingUp => pickupState == PickupState.PickingUp;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        interactionUI = InteractionUI.Instance;
        playerCollider = GetComponent<Collider>();

        if (interactionUI == null)
        {
            Debug.LogWarning(
                "PickupController could not find InteractionUI.Instance."
            );
        }

        if (interactionDetector == null)
        {
            Debug.LogWarning(
                "PickupController has no InteractionDetector assigned."
            );
        }

        if (grabPosition == null)
        {
            Debug.LogWarning(
                "PickupController has no GrabPosition assigned."
            );
        }

        if (playerView == null)
        {
            Debug.LogWarning(
                "PickupController has no Player View assigned."
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
        }

        FollowHeldObject();
    }

    // ---------------------------------------------------------
    // IDLE
    // ---------------------------------------------------------

    private void HandleIdleInput()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        GrabbableObject target = GetCurrentGrabbable();

        if (target == null)
            return;

        StartPickup(target);
    }

    private void StartPickup(GrabbableObject target)
    {
        pickupTarget = target;
        pickupTimer = 0f;

        if (target.PickupTime <= 0f)
        {
            PickUpObject(target);
            return;
        }

        pickupState = PickupState.PickingUp;
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

        GrabbableObject currentTarget = GetCurrentGrabbable();

        if (currentTarget != pickupTarget)
        {
            CancelPickup();
            return;
        }

        pickupTimer += Time.deltaTime;

        if (pickupTimer >= pickupTarget.PickupTime)
        {
            PickUpObject(pickupTarget);
        }
    }

    private void CancelPickup()
    {
        pickupTarget = null;
        pickupTimer = 0f;
        pickupState = PickupState.None;
    }

    // ---------------------------------------------------------
    // HOLDING
    // ---------------------------------------------------------

    private void HandleHeldInput()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        DropObject();
    }

    private void PickUpObject(GrabbableObject objectToPickup)
    {
        Rigidbody rigidbody =
            objectToPickup.GetComponent<Rigidbody>();

        if (rigidbody == null)
        {
            Debug.LogWarning(
                $"GrabbableObject '{objectToPickup.name}' has no Rigidbody."
            );

            CancelPickup();
            return;
        }

        heldObject = objectToPickup;
        heldRigidbody = rigidbody;

        heldRigidbody.linearVelocity = Vector3.zero;
        heldRigidbody.angularVelocity = Vector3.zero;
        heldRigidbody.isKinematic = true;

        ConfigureHeldCollision();

        pickupTarget = null;
        pickupTimer = 0f;
        pickupState = PickupState.Held;

        if (interactionUI != null)
            interactionUI.HidePrompt();
    }

    private void FollowHeldObject()
    {
        if (pickupState != PickupState.Held)
            return;

        if (heldObject == null || heldRigidbody == null)
            return;

        if (grabPosition == null)
            return;

        // Position
        Vector3 targetPosition =
            grabPosition.position +
            grabPosition.TransformVector(heldObject.HeldOffset);

        heldRigidbody.MovePosition(
            Vector3.Lerp(
                heldRigidbody.position,
                targetPosition,
                holdFollowSpeed * Time.deltaTime
            )
        );

        // Rotation
        if (playerView != null)
        {
            float playerYaw = playerView.transform.eulerAngles.y;

            Quaternion targetRotation =
                Quaternion.Euler(0f, playerYaw, 0f);

            heldRigidbody.MoveRotation(
                Quaternion.Lerp(
                    heldRigidbody.rotation,
                    targetRotation,
                    holdFollowSpeed * Time.deltaTime
                )
            );
        }
    }

    private void DropObject()
    {
        if (heldRigidbody == null)
        {
            ClearHeldObject();
            return;
        }

        RestoreHeldCollision();

        heldRigidbody.isKinematic = false;
        heldRigidbody.linearVelocity = Vector3.zero;
        heldRigidbody.angularVelocity = Vector3.zero;

        ClearHeldObject();
    }

    private void ClearHeldObject()
    {
        heldObject = null;
        heldRigidbody = null;
        heldColliders = null;

        pickupState = PickupState.None;
    }

    // ---------------------------------------------------------
    // DETECTION
    // ---------------------------------------------------------

    private GrabbableObject GetCurrentGrabbable()
    {
        if (interactionDetector == null)
            return null;

        Interactable target =
            interactionDetector.CurrentTarget;

        if (target == null)
            return null;

        if (target.Type != Interactable.InteractionType.Grabbable)
            return null;

        return target.GetComponent<GrabbableObject>();
    }

    // ---------------------------------------------------------
    // COLLISION
    // ---------------------------------------------------------

    private void ConfigureHeldCollision()
    {
        if (heldObject == null)
            return;

        heldColliders =
            heldObject.GetComponentsInChildren<Collider>();

        if (!heldObject.WorldCollisionWhileHeld)
        {
            foreach (Collider objectCollider in heldColliders)
            {
                if (objectCollider == null)
                    continue;

                objectCollider.enabled = false;
            }

            return;
        }

        // World collision stays enabled.
        // Only ignore collision with the holder.
        if (playerCollider == null)
            return;

        foreach (Collider objectCollider in heldColliders)
        {
            if (objectCollider == null)
                continue;

            Physics.IgnoreCollision(
                playerCollider,
                objectCollider,
                true
            );
        }
    }

    private void RestoreHeldCollision()
    {
        if (heldObject == null || heldColliders == null)
            return;

        if (!heldObject.WorldCollisionWhileHeld)
        {
            foreach (Collider objectCollider in heldColliders)
            {
                if (objectCollider == null)
                    continue;

                objectCollider.enabled = true;
            }

            return;
        }

        if (playerCollider == null)
            return;

        foreach (Collider objectCollider in heldColliders)
        {
            if (objectCollider == null)
                continue;

            Physics.IgnoreCollision(
                playerCollider,
                objectCollider,
                false
            );
        }
    }

    // ---------------------------------------------------------
    // UI
    // ---------------------------------------------------------

    private void UpdateInteractionUI()
    {
        if (interactionUI == null)
            return;

        if (pickupState == PickupState.Held)
        {
            interactionUI.HidePrompt();
            return;
        }

        if (pickupState == PickupState.PickingUp)
        {
            interactionUI.HidePrompt();
            return;
        }

        GrabbableObject target = GetCurrentGrabbable();

        if (target != null)
            interactionUI.ShowPickupPrompt(target);
        else
            interactionUI.HidePrompt();
    }
}