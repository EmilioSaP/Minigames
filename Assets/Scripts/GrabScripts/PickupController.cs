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

    [Header("Held Object")]
    [SerializeField] private float holdFollowSpeed = 15f;

    [Header("Throw")]
    [SerializeField] private float minimumThrowCharge = 0.1f;

    private InteractionUI interactionUI;

    private GrabbableObject heldObject;
    private Rigidbody heldRigidbody;

    private PickupState pickupState = PickupState.None;

    private GrabbableObject pickupTarget;
    private float pickupTimer;

    private float throwChargeTimer;

    private Collider[] heldColliders;
    private Collider playerCollider;

    public bool IsHoldingObject => heldObject != null;
    public bool IsPickingUp => pickupState == PickupState.PickingUp;
    public bool IsChargingThrow => pickupState == PickupState.ChargingThrow;

    public float ThrowChargeNormalized
    {
        get
        {
            if (heldObject == null)
                return 0f;

            if (heldObject.ThrowChargeTime <= 0f)
                return 1f;

            return Mathf.Clamp01(
                throwChargeTimer / heldObject.ThrowChargeTime
            );
        }
    }

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

            case PickupState.ChargingThrow:
                HandleThrowCharge();
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
    // HOLDING / THROWING
    // ---------------------------------------------------------

    private void HandleHeldInput()
    {
        // Pressing and holding the button starts charging.
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            StartThrowCharge();
        }
    }

    private void StartThrowCharge()
    {
        if (heldObject == null)
            return;

        throwChargeTimer = 0f;

        // No charging time means full power immediately.
        if (heldObject.ThrowChargeTime <= 0f)
        {
            throwChargeTimer = 0f;
        }

        pickupState = PickupState.ChargingThrow;
    }

    private void HandleThrowCharge()
    {
        if (heldObject == null || heldRigidbody == null)
        {
            ClearHeldObject();
            return;
        }

        // Keep charging while the button is held.
        if (Mouse.current.leftButton.isPressed)
        {
            if (heldObject.ThrowChargeTime > 0f)
            {
                throwChargeTimer += Time.deltaTime;

                // Clamp the timer so it never grows indefinitely.
                throwChargeTimer = Mathf.Min(
                    throwChargeTimer,
                    heldObject.ThrowChargeTime
                );
            }

            return;
        }

        // Button was released.
        ReleaseThrow();
    }

    private void ReleaseThrow()
    {
        if (heldObject == null || heldRigidbody == null)
        {
            ClearHeldObject();
            return;
        }

        float normalizedCharge = GetThrowCharge();
        Vector3 throwDirection = GetThrowDirection();

        // A tiny tap is considered a normal drop.
        if (normalizedCharge < minimumThrowCharge)
        {
            DropObject();
            return;
        }

        ThrowObject(
            throwDirection,
            normalizedCharge
        );
    }

    private float GetThrowCharge()
    {
        if (heldObject == null)
            return 0f;

        if (heldObject.ThrowChargeTime <= 0f)
            return 1f;

        return Mathf.Clamp01(
            throwChargeTimer / heldObject.ThrowChargeTime
        );
    }

    private Vector3 GetThrowDirection()
    {
        if (playerView == null)
            return transform.forward;

        return playerView.transform.forward.normalized;
    }

    private void ThrowObject(
        Vector3 direction,
        float normalizedCharge)
    {
        float throwForce =
            normalizedCharge * heldObject.MaxThrowForce;

        RestoreHeldCollision();

        // Return the object to normal physics first.
        heldRigidbody.isKinematic = false;

        heldRigidbody.linearVelocity = Vector3.zero;
        heldRigidbody.angularVelocity = Vector3.zero;

        // Apply the actual throw.
        heldRigidbody.AddForce(
            direction * throwForce,
            ForceMode.Impulse
        );

        ClearHeldObject();
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
        throwChargeTimer = 0f;

        pickupState = PickupState.Held;

        if (interactionUI != null)
            interactionUI.HidePrompt();
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

        pickupTarget = null;
        pickupTimer = 0f;
        throwChargeTimer = 0f;

        pickupState = PickupState.None;
    }

    // ---------------------------------------------------------
    // HELD OBJECT FOLLOWING
    // ---------------------------------------------------------

    private void FollowHeldObject()
    {
        if (heldObject == null || heldRigidbody == null)
            return;

        if (grabPosition == null)
            return;

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

        if (pickupState == PickupState.Held ||
            pickupState == PickupState.ChargingThrow)
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